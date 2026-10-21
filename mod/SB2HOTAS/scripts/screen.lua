-- SB2 HOTAS: wide and multi-monitor screens for SpaceBourne 2.
-- The bridge's "Triple screen" tab stretches the game window over every monitor and writes screen.txt,
-- four numbers: fov hud left width
--   fov          1 = Hor+: on a screen wider than 16:9 the sides show more of the world instead of zooming in.
--   hud          1 = HUD and menus stay on the centre monitor.
--   left width   that monitor's slice of the window, as fractions; width 0 = a centred 16:9 slice.
-- Without the file (bridge never opened) nothing changes. Each fix switches itself off if the game changes
-- under it, and neither ever touches flight.
local Config = require("config")
local M = {}
local WIDE = 16 / 9
local FULL = {Minimum = {X = 0, Y = 0}, Maximum = {X = 1, Y = 1}}
-- SpaceBourne 2 sizes most of its widgets to the whole window itself (SetDesiredSizeInViewport, anchored at the
-- top-left corner) instead of stretching them. These are resized to the centre monitor; the SetDesiredSizeInViewport
-- hook below also spots others that get the whole window.
local SIZED = {W_Main_Menu_C = true, Pause_Menu_C = true, Ship_MainWidget_C = true, WBP_LogHUD_C = true,
    CPP_WArningWidget_C = true}
-- These draw things where they are on screen (target markers, the aim crosshair, tooltips at the mouse), so they
-- must keep covering every monitor.
local KEEP = {MainIndicatorHUD_C = true, Test_Crosshair_C = true, SMW_ToolTipHolder_C = true}
local wholeWindow = {}  -- widgets the game sized to the whole window: address -> true
local opts = {fov = false, hud = false, left = 0, width = 0}   -- all off until the bridge's tab turns them on
local off = {}         -- fixes that hit an error: name -> true
local moved = {}       -- widgets we put on the centre monitor: address -> {widget, left, width, sized}
local layout, pc, locked, lastNote
local smooth, smoothAt = nil, 0   -- the game's own FOV, smoothed (see fov())

local function valid(o) return o ~= nil and o:IsValid() end
local function near(a, b) return math.abs(a - b) < 1e-4 end
local function note(text)
    if text ~= lastNote then lastNote = text; print("[SB2HOTAS] Screen: " .. text .. "\n") end
end
local function guard(name, fn)
    if off[name] then return end
    local ok, err = pcall(fn)
    if not ok then off[name] = true; note(name .. " fix switched off: " .. tostring(err)) end
end

local function readOptions()
    local f = io.open(Config.screen, "r")
    if not f then return end
    local text = f:read(200) or ""
    f:close()
    local v = {}
    for token in text:gmatch("%S+") do v[#v + 1] = tonumber(token) end
    if #v ~= 4 then return end
    for i = 1, 4 do if v[i] == nil or v[i] ~= v[i] or v[i] < 0 or v[i] > 1 then return end end
    opts.fov, opts.hud, opts.left, opts.width = v[1] == 1, v[2] == 1, v[3], v[4]
end

local nextSearch = 0
local function controller()
    if valid(pc) then return pc end
    pc, locked, smooth = nil, nil, nil
    if os.clock() < nextSearch then return nil end   -- loading or main menu: look once a second, not every frame
    nextSearch = os.clock() + 1
    for _, c in ipairs(FindAllOf("PlayerController") or {}) do
        if valid(c) and c:IsLocalPlayerController() then pc = c; break end
    end
    return pc
end

local function viewport()
    layout = layout or StaticFindObject("/Script/UMG.Default__WidgetLayoutLibrary")
    if not valid(layout) then error("UMG layout library not found") end
    local size = layout:GetViewportSize(pc)
    return size.X, size.Y
end

-- The game keeps the horizontal FOV fixed, so a wider window would just crop the top and bottom.
-- Widen it so the vertical view stays what it is at 16:9. PlayerController.FOV locks the camera FOV (0 unlocks);
-- the camera cache still holds the game's own value every frame, so zoom and boost effects keep working.
local function fov()
    local c = controller()
    if not c then return end
    local camera = c.PlayerCameraManager
    if not valid(camera) then return end
    local pov = camera.CameraCachePrivate.POV
    local base = pov.FOV
    if type(base) ~= "number" then error("incompatible game version (camera FOV)") end
    local w, h = viewport()
    local aspect = h > 0 and w / h or WIDE
    if pov.bConstrainAspectRatio and pov.AspectRatio > 0 then aspect = math.min(aspect, pov.AspectRatio) end
    local player = c.Player
    local horPlus = valid(player) and player.AspectRatioAxisConstraint == 0   -- the game already keeps vertical FOV
    local want = 0
    if opts.fov and not horPlus and aspect > WIDE + .01 and base > 1 and base < 170 then
        if locked and locked > 0 and near(base, locked) then return end    -- our own lock read back: nothing new
        -- The game wobbles its FOV by about half a degree every frame (90.3-90.8°; unseen on one screen). Widened to
        -- 140° and more, that wobble shows as a shimmer, so follow it smoothly; a change of more than 3° (zoom,
        -- another camera) at once.
        local now = os.clock()
        if not smooth or math.abs(base - smooth) > 3 then smooth = base
        else smooth = smooth + (base - smooth) * math.min(1, (now - smoothAt) / .5) end
        smoothAt = now
        want = math.deg(2 * math.atan(math.tan(math.rad(smooth) / 2) * aspect / WIDE))
        note(string.format("%dx%d, field of view widened (Hor+)", math.floor(w), math.floor(h)))
    else
        smooth = nil
    end
    if locked == nil or math.abs(want - locked) > .05 or (want == 0 and locked ~= 0) then
        c:FOV(want); locked = want
        -- Diagnostics: logs only if the lock moves almost every frame (a shimmer), with the game's own FOV range.
        local now = os.clock()
        M.stat = M.stat or {start = now, n = 0, lo = base, hi = base}
        local s = M.stat
        s.n, s.lo, s.hi = s.n + 1, math.min(s.lo, base), math.max(s.hi, base)
        if now - s.start >= 5 then
            if s.n > 150 then print(string.format("[SB2HOTAS] Screen: FOV moved %d times in 5 s (game FOV %.2f..%.2f)\n", s.n, s.lo, s.hi)) end
            M.stat = nil
        end
    end
end

local function slice()
    if opts.width > 0 then return opts.left, opts.width end
    local w, h = viewport()
    if w <= 0 or h <= 0 then return 0, 1 end
    local width = math.min(1, WIDE * h / w)
    return (1 - width) / 2, width
end

local function restore(m)
    if not valid(m.widget) then return end
    if m.sized then
        local w, h = viewport()
        local scale = layout:GetViewportScale(pc)
        if scale > 0 then
            m.widget:SetDesiredSizeInViewport({X = w / scale, Y = h / scale})
            m.widget:SetPositionInViewport({X = 0, Y = 0}, true)
        end
    else
        m.widget:SetAnchorsInViewport(FULL)
    end
end

-- Full-screen widgets (HUD, menus) get anchored to the centre monitor's slice. Widgets the game places itself
-- (markers, tooltips, anything moved with SetPositionInViewport) keep their spot, except the SIZED menus.
local logged = {}      -- widget classes already reported in UE4SS.log
local function report(class, text)
    if not logged[class] then logged[class] = true; print("[SB2HOTAS] Screen: " .. class .. " " .. text .. "\n") end
end
local function place(widget, left, width)
    local key = widget:GetAddress()
    local mine = moved[key]
    local want = opts.hud and width < .999
    local class = widget:GetClass():GetFName():ToString()
    if KEEP[class] then
        if mine then restore(mine); moved[key] = nil end
        return
    end
    if SIZED[class] or wholeWindow[key] then
        if want then report(class, "resized to the centre monitor") end
        if want then
            local w, h = viewport()
            local scale = layout:GetViewportScale(pc)
            if scale <= 0 then return end
            widget:SetDesiredSizeInViewport({X = width * w / scale, Y = h / scale})
            widget:SetPositionInViewport({X = left * w, Y = 0}, true)
            moved[key] = {widget = widget, left = left, width = width, sized = true}
        elseif mine then
            restore(mine)
            moved[key] = nil
        end
        return
    end
    local a = widget:GetAnchorsInViewport()
    if mine and not (near(a.Minimum.X, mine.left) and near(a.Maximum.X, mine.left + mine.width)) then
        moved[key], mine = nil, nil                  -- the game has placed it itself since
    end
    local full = a.Minimum.X == 0 and a.Minimum.Y == 0 and a.Maximum.X == 1 and a.Maximum.Y == 1
    if not mine and not full then
        if want then report(class .. "#", string.format("left in place (the game anchors it at %.2f,%.2f-%.2f,%.2f)", a.Minimum.X, a.Minimum.Y, a.Maximum.X, a.Maximum.Y)) end
        return
    end
    if want then
        report(class, "moved to the centre monitor")
        if not mine or not near(mine.left, left) or not near(mine.width, width) then
            widget:SetAnchorsInViewport({Minimum = {X = left, Y = 0}, Maximum = {X = left + width, Y = 1}})
            moved[key] = {widget = widget, left = left, width = width}
        end
    elseif mine then
        restore(mine)
        moved[key] = nil
    end
end

local function hud()
    if not opts.hud then
        if next(moved) and controller() then
            for key, m in pairs(moved) do restore(m); moved[key] = nil end
        end
        return
    end
    if not controller() then return end
    local left, width = slice()
    for _, w in ipairs(FindAllOf("UserWidget") or {}) do
        if valid(w) and w:IsInViewport() then place(w, left, width) end
    end
    for key, m in pairs(moved) do if not valid(m.widget) then moved[key] = nil end end
end

-- Called by the flight hook on every frame of the player's ship, so FOV effects stay smooth in flight.
function M.frame() guard("FOV", fov) end

function M.start()
    readOptions()
    -- New HUD and menu widgets go to the centre monitor as soon as they appear.
    for _, name in ipairs({"AddToViewport", "AddToPlayerScreen"}) do
        pcall(RegisterHook, "/Script/UMG.UserWidget:" .. name, function(context)
            if not opts.hud then return end
            guard("HUD", function()
                local w = context:get()
                if valid(w) and controller() then place(w, slice()) end
            end)
        end)
    end
    -- Learn which widgets the game sizes to the whole window, so menus not listed in SIZED follow too.
    pcall(RegisterHook, "/Script/UMG.UserWidget:SetDesiredSizeInViewport", function(context, size)
        if not opts.hud then return end
        guard("HUD", function()
            local w = context:get()
            if not valid(w) or not controller() then return end
            local s = size:get()
            local vw, vh = viewport()
            local scale = layout:GetViewportScale(pc)
            if scale > 0 and math.abs(s.X - vw / scale) < 2 and math.abs(s.Y - vh / scale) < 2 then
                wholeWindow[w:GetAddress()] = true
                report(w:GetClass():GetFName():ToString() .. "+", "is sized to the whole window by the game")
            end
        end)
    end)
    local ticks, queued = 0, false
    LoopAsync(100, function()
        if queued then return false end
        queued = true
        ExecuteInGameThread(function()
            ticks = ticks + 1
            pcall(function()
                if ticks % 10 == 1 then readOptions(); guard("HUD", hud) end
                guard("FOV", fov)
            end)
            queued = false
        end)
        return false
    end)
end

return M
