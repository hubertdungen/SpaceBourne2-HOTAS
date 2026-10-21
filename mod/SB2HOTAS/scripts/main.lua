-- SB2 HOTAS: analog joystick/pedal flight and head tracking for SpaceBourne 2, fed by the SB2-HOTAS bridge.
-- No offsets, executable patches, save writes, or console gameplay commands.
local P = require("protocol")
local Config = require("config")
local Screen = require("screen")   -- triple screen / ultrawide: FOV and HUD, independent of flight
-- Game internals this mod relies on. If an update renames one, the mod switches itself off and its
-- status (bridge "In-game mod" line, UE4SS.log) names the missing property; fix the name here.
-- Blueprint classes:
local SHIP = "/Game/1Data/SB2Ships/Q1_Master_Ship.Q1_Master_Ship_C"
local COCKPIT = "/Game/1Data/SB2Ships/Cockpit/Q1_Master_Cockpit.Q1_Master_Cockpit_C"
-- Ship flags meaning "not in free flight"; while any is true, control goes back to the game.
local BLOCKERS = {"DisableALL","DisableMovement?","DisableAxisMovement","CursorOn?",
    "RadialOpened?","Landed?","Docking?","Warp_Active?","Warp_Drive_On?",
    "PlayerEnteringSurface","CapitalMode","TakingOff","VerticleTakeOff","EngineOff?"}
-- Also used below: ship SpringArm, SixDOFMovement (SetForward/Right/UpInput), MoveRoll, ThrustLocked, UpdateVectors(),
-- "CockpitOn?", Controller; cockpit ShipRef, Camera, ConsoleOn, ServiceOpened.
-- Never keep a game object from one tick to the next: once the game destroys it (loading a save, changing ship),
-- UE4SS can't always tell, and touching it crashes the game. Only addresses are kept, and objects are only
-- touched through the hook that hands them over.
local flying, cameraState, failed = nil, nil, false   -- flying: address of the ship we steer
local shipWatch, headWatch = P.watchdog(), P.watchdog()
local hookedShip, hookedCamera = false, false
local lastStatus = ""
local function valid(o) return o ~= nil and o:IsValid() end
local function status(message)
    if message == lastStatus then return end
    lastStatus=message
    print("[SB2HOTAS] " .. message .. "\n")
    local f=io.open(Config.status,"w")
    if f then f:write(message); f:close() end
end
local lastFrame
local function read()
    local f=io.open(Config.state,"r")
    if f then
        local s=f:read(1025); f:close()
        lastFrame=P.parse(s,os.time()) or lastFrame
    end
    -- A failed read (bridge replacing the file) reuses the last frame; its unchanged seq still trips the watchdog.
    return lastFrame
end
-- Hands the ship back to the game, if it's the one we steer. `ship` comes from the current hook.
local function releaseFlight(ship)
    if not flying or ship:GetAddress()~=flying then return end
    local m=ship.SixDOFMovement
    if valid(m) then
        m:SetForwardInput(0); m:SetRightInput(0); m:SetUpInput(0)
    end
    ship.MoveRoll=0
    flying=nil
end
-- Puts the cockpit camera back where the game had it, if it's the one we moved. `cam` comes from the current hook.
local function releaseCamera(cam)
    if not cameraState or cam:GetAddress()~=cameraState.key then return end
    cam:K2_SetRelativeRotation(cameraState.rotation,false,{},false)
    cam:K2_SetRelativeLocation(cameraState.location,false,{},false)
    cameraState=nil
end
-- After an error the mod stops, but each hook still hands its object back to the game (cleanup).
local function protect(fn,cleanup)
    if failed then pcall(cleanup); return end
    local ok,err=pcall(fn)
    if not ok then
        failed=true
        pcall(cleanup)
        status("ERROR: mod disabled until the game restarts: "..tostring(err))
    end
end
local function allowed(ship)
    if not valid(ship) or not ship:IsPlayerControlled() then return false end
    -- Fail closed if a required reflected property changes in a future build.
    for _,key in ipairs(BLOCKERS) do
        local value=ship[key]
        if type(value)~="boolean" then error("incompatible game version (property "..key..")") end
        if value then return false end
    end
    local controller=ship.Controller
    if not valid(controller) or controller.bShowMouseCursor then return false end
    return true
end
local function flight(context,delta)
    pcall(Screen.tick)   -- once a second, from any ship: HUD placement and FOV outside the cockpit
    local function cleanup() local s=context:get(); if valid(s) then releaseFlight(s) end end
    protect(function()
        local ship=context:get()
        if not valid(ship) then return end
        if not ship:IsPlayerControlled() then releaseFlight(ship); return end   -- the ship we steered lost its pilot
        Screen.frame()   -- has its own error guard, so it can never switch flight off
        local dt=tonumber(delta:get()) or 0
        local f=read()
        local fresh=shipWatch(f,dt)
        if not fresh or not f.flight or not allowed(ship) then
            releaseFlight(ship)
            status(fresh and "Connected; flight on hold (bridge disarmed, menu, or automatic manoeuvre)."
                or "Waiting for the bridge: no new frames (open the SB2 HOTAS bridge and click ARM).")
            return
        end
        flying=ship:GetAddress()   -- a ship we flew before and no longer see is left alone: it may be gone
        dt=P.clamp(dt,0,.05)
        local arm=ship.SpringArm
        local movement=ship.SixDOFMovement
        if not valid(arm) or not valid(movement) then error("flight components unavailable") end
        if type(ship.MoveRoll)~="number" then error("incompatible game version (property MoveRoll)") end
        local has=f.has
        if has.pitch or has.yaw then
            -- Same aim arm as the game's retained HOTAS path; cockpit head camera is separate.
            -- Arm pitch up = aim up (as MouseY), so stick forward (negative) dives.
            arm:K2_AddLocalRotation({Pitch=f.pitch*f.rate*dt,Yaw=f.yaw*f.rate*dt,Roll=0},false,{},false)
            ship:UpdateVectors()
        end
        -- Axes the player did not assign are left alone, so keyboard and mouse keep driving them.
        if type(ship.ThrustLocked)~="boolean" then error("incompatible game version (property ThrustLocked)") end
        -- The game's speed lock (Caps Lock) always wins: it lets a spring-centred HOSAS stick or the keyboard hold a speed.
        if has.throttle and not ship.ThrustLocked then movement:SetForwardInput(f.throttle) end
        if has.strafe then movement:SetRightInput(f.strafe) end
        if has.lift then movement:SetUpInput(f.lift) end
        -- SetRollInput takes a per-frame amount, not an axis. MoveRoll is the target the roll keys
        -- set (right = -1, left = +1); the next ship tick smooths it and rolls ship and aim arm.
        if has.roll then ship.MoveRoll=-f.roll end
        status("Analog flight active.")
    end,cleanup)
end
local function head(context,delta)
    pcall(Screen.tick)
    local function cleanup()
        local cockpit=context:get()
        local cam=valid(cockpit) and cockpit.Camera or nil
        if valid(cam) then releaseCamera(cam) end
    end
    protect(function()
        local cockpit=context:get()
        if not valid(cockpit) then return end
        local cam=cockpit.Camera
        if not valid(cam) then return end
        local ship=cockpit.ShipRef
        if not valid(ship) or not ship:IsPlayerControlled() then releaseCamera(cam); return end
        local dt=tonumber(delta:get()) or 0
        local f=read()
        if not headWatch(f,dt) or not f.head or not ship["CockpitOn?"] or not allowed(ship)
            or cockpit.ConsoleOn or cockpit.ServiceOpened then
            releaseCamera(cam); return
        end
        -- The camera's own pose is remembered per camera and per controller: after loading a save, a new camera can
        -- get the old one's address, and a new controller tells the two apart.
        local key,owner=cam:GetAddress(),ship.Controller:GetAddress()
        if cameraState and (cameraState.key~=key or cameraState.owner~=owner) then cameraState=nil end
        if not cameraState then
            local r,l=cam.RelativeRotation,cam.RelativeLocation
            cameraState={key=key,owner=owner,rotation={Pitch=r.Pitch,Yaw=r.Yaw,Roll=r.Roll},location={X=l.X,Y=l.Y,Z=l.Z}}
        end
        local r,l=cameraState.rotation,cameraState.location
        cam:K2_SetRelativeRotation({Pitch=r.Pitch+P.clamp(f.headPitch,-75,75),Yaw=r.Yaw+P.clamp(f.headYaw,-110,110),Roll=r.Roll+P.clamp(f.headRoll,-35,35)},false,{},false)
        -- OpenTrack X=right,Y=up,Z=back; UE X=forward,Y=right,Z=up, both in cm.
        cam:K2_SetRelativeLocation({X=l.X-P.clamp(f.z,-20,20),Y=l.Y+P.clamp(f.x,-20,20),Z=l.Z+P.clamp(f.y,-15,15)},false,{},false)
    end,cleanup)
end
status("Loaded. Waiting for a player-controlled ship.")
-- The ship and cockpit classes only exist once the game has loaded them, so the hooks go in as soon as they do.
local function tryHooks()
    if failed or (hookedShip and hookedCamera) then return end
    protect(function()
        if not hookedShip and valid(StaticFindObject(SHIP..":ReceiveTick")) then
            RegisterHook(SHIP..":ReceiveTick",flight); hookedShip=true
            print("[SB2HOTAS] Flight hook registered\n")
        end
        if not hookedCamera and valid(StaticFindObject(COCKPIT..":ReceiveTick")) then
            RegisterHook(COCKPIT..":ReceiveTick",head); hookedCamera=true
            print("[SB2HOTAS] Cockpit hook registered\n")
        end
    end,function() end)
end
-- Menus have no ship ticking, so Screen also follows the game's own calls that put a widget on screen, size, move
-- or anchor it: right after the game does, Screen puts that widget on the centre monitor, before it is ever drawn
-- elsewhere. These are engine functions, hooked once; the game calls them on the game thread.
-- true: only widgets Screen placed itself (the game calls these a lot); false: any widget.
local WIDGET_CALLS={AddToViewport=false,AddToPlayerScreen=false,SetDesiredSizeInViewport=false,
    SetPositionInViewport=true,SetAnchorsInViewport=true}
local widgetsHooked=false
local function hookWidgets()
    if widgetsHooked then return end
    widgetsHooked=true
    for name,onlyMoved in pairs(WIDGET_CALLS) do
        local fn="/Script/UMG.UserWidget:"..name
        local sizing=name=="SetDesiredSizeInViewport"   -- its size tells Screen which menus fill the whole window
        local ok,err=pcall(function()
            if not valid(StaticFindObject(fn)) then error("not found") end
            RegisterHook(fn,function() end,function(context,size)
                pcall(Screen.widget,context:get(),onlyMoved,sizing and size or nil)
            end)
        end)
        print("[SB2HOTAS] Screen "..(ok and "follows " or "can't follow ")..fn..(ok and "" or ": "..tostring(err)).."\n")
    end
end
-- The main menu's level Blueprint ticks every frame, so it drives Screen there too (once a second): the window
-- may be stretched after the menu is up. Any level whose Blueprint has a Tick gets this, one hook per level class.
local levelBase,levelHooked=nil,{}
local function hookLevelTick(actor)
    levelBase=levelBase or StaticFindObject("/Script/Engine.LevelScriptActor")
    if not valid(levelBase) or not valid(actor) or not actor:IsA(levelBase) then return end
    local path=actor:GetClass():GetFullName():match("^%S+%s+(/Game/.+)$")
    if not path or levelHooked[path]~=nil then return end
    local fn=path..":ReceiveTick"
    levelHooked[path]=valid(StaticFindObject(fn)) and pcall(RegisterHook,fn,function() pcall(Screen.tick) end) or false
    if levelHooked[path] then print("[SB2HOTAS] Screen also ticks with "..fn.."\n") end
end
-- Try on the game thread whenever an actor starts or a map's game state is set up. A background loop
-- (LoopAsync) is only the fallback for UE4SS builds without these hooks: Lua must never run on two threads at once.
local onGameThread=false
if type(RegisterBeginPlayPostHook)=="function" and pcall(RegisterBeginPlayPostHook,function(context)
    tryHooks(); hookWidgets(); pcall(hookLevelTick,context:get()); pcall(Screen.tick)
end) then onGameThread=true end
if type(RegisterInitGameStatePostHook)=="function" and pcall(RegisterInitGameStatePostHook,function()
    tryHooks(); hookWidgets(); pcall(Screen.tick)
end) then onGameThread=true end
if not onGameThread then
    local queued=false
    LoopAsync(1000,function()
        if failed or (hookedShip and hookedCamera) then return true end
        if queued then return false end
        queued=true
        ExecuteInGameThread(function() tryHooks(); queued=false end)
        return false
    end)
end
Screen.start()
