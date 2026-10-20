-- SB2 HOTAS: analog joystick/pedal flight and head tracking for SpaceBourne 2, fed by the SB2-HOTAS bridge.
-- No offsets, executable patches, save writes, or console gameplay commands.
local P = require("protocol")
local Config = require("config")
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
local lastFlight, cameraState, failed = nil, nil, false
local shipWatch, headWatch = P.watchdog(), P.watchdog()
local hookedShip, hookedCamera = false, false
local lastStatus = ""
local function valid(o) return o ~= nil and o:IsValid() end
local function status(message)
    if message == lastStatus then return end
    lastStatus=message
    print("[SB2HOTAS] " .. message .. "\n")
    local f=io.open(Config.status, "w")
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
local function releaseFlight()
    if valid(lastFlight) then
        local m=lastFlight.SixDOFMovement
        if valid(m) then
            m:SetForwardInput(0); m:SetRightInput(0); m:SetUpInput(0)
        end
        lastFlight.MoveRoll=0
    end
    lastFlight=nil
end
local function releaseCamera()
    if cameraState and valid(cameraState.camera) then
        cameraState.camera:K2_SetRelativeRotation(cameraState.rotation,false,{},false)
        cameraState.camera:K2_SetRelativeLocation(cameraState.location,false,{},false)
    end
    cameraState=nil
end
local function protect(fn)
    if failed then return end
    local ok,err=pcall(fn)
    if not ok then
        failed=true
        pcall(releaseFlight); pcall(releaseCamera)
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
    protect(function()
        local ship=context:get()
        if not valid(ship) or not ship:IsPlayerControlled() then return end
        local dt=tonumber(delta:get()) or 0
        local f=read()
        if not shipWatch(f,dt) or not f.flight or not allowed(ship) then
            releaseFlight()
            status("Connected; flight on hold (bridge disarmed, menu, or automatic manoeuvre).")
            return
        end
        if lastFlight and lastFlight:GetAddress()~=ship:GetAddress() then releaseFlight() end
        lastFlight=ship
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
    end)
end
local function head(context,delta)
    protect(function()
        local cockpit=context:get()
        if not valid(cockpit) then return end
        local ship=cockpit.ShipRef
        if not valid(ship) or not ship:IsPlayerControlled() then return end
        local dt=tonumber(delta:get()) or 0
        local f=read()
        if not headWatch(f,dt) or not f.head or not ship["CockpitOn?"] or not allowed(ship)
            or cockpit.ConsoleOn or cockpit.ServiceOpened then
            releaseCamera(); return
        end
        local cam=cockpit.Camera
        if not valid(cam) then return end
        if cameraState and cameraState.camera:GetAddress()~=cam:GetAddress() then releaseCamera() end
        if not cameraState then
            local r,l=cam.RelativeRotation,cam.RelativeLocation
            cameraState={camera=cam,rotation={Pitch=r.Pitch,Yaw=r.Yaw,Roll=r.Roll},location={X=l.X,Y=l.Y,Z=l.Z}}
        end
        local r,l=cameraState.rotation,cameraState.location
        cam:K2_SetRelativeRotation({Pitch=r.Pitch+P.clamp(f.headPitch,-75,75),Yaw=r.Yaw+P.clamp(f.headYaw,-110,110),Roll=r.Roll+P.clamp(f.headRoll,-35,35)},false,{},false)
        -- OpenTrack X=right,Y=up,Z=back; UE X=forward,Y=right,Z=up, both in cm.
        cam:K2_SetRelativeLocation({X=l.X-P.clamp(f.z,-20,20),Y=l.Y+P.clamp(f.x,-20,20),Z=l.Z+P.clamp(f.y,-15,15)},false,{},false)
    end)
end
status("Loaded. Waiting for a player-controlled ship.")
local queued=false
LoopAsync(1000,function()
    if failed or (hookedShip and hookedCamera) then return true end
    if queued then return false end
    queued=true
    ExecuteInGameThread(function()
        protect(function()
            if not hookedShip and valid(StaticFindObject(SHIP..":ReceiveTick")) then
                RegisterHook(SHIP..":ReceiveTick",flight); hookedShip=true
                print("[SB2HOTAS] Flight hook registered\n")
            end
            if not hookedCamera and valid(StaticFindObject(COCKPIT..":ReceiveTick")) then
                RegisterHook(COCKPIT..":ReceiveTick",head); hookedCamera=true
                print("[SB2HOTAS] Cockpit hook registered\n")
            end
        end)
        queued=false
    end)
    return false
end)
