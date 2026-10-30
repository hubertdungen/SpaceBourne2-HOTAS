-- SB2 HOTAS: wide and multi-monitor screens for SpaceBourne 2.
-- The bridge's "Triple screen" tab stretches the game window over every monitor and writes screen.txt,
-- four numbers: fov hud left width
--   fov          1 = Hor+: on a screen wider than 16:9 the sides show more of the world instead of zooming in.
--   hud          1 = HUD and menus stay on the centre monitor.
--   left width   that monitor's slice of the window, as fractions; width 0 = a centred 16:9 slice.
-- Without the file (bridge never opened) nothing changes. Each fix switches itself off if the game changes
-- under it, and neither ever touches flight.
-- Never keep a game object from one call to the next: once the game destroys it (loading a save, changing area),
-- UE4SS can't always tell, and touching it crashes the game. Only addresses are kept; objects come fresh from the
-- game instance and the engine's default objects, which last as long as the game.
local Config = require("config")
local M = {}
local WIDE = 16 / 9
-- SpaceBourne 2 sizes most of its widgets to the whole window itself (SetDesiredSizeInViewport, anchored at the
-- top-left corner) instead of stretching them. These are resized to the centre monitor.
local SIZED = {W_Main_Menu_C = true, Pause_Menu_C = true, Ship_MainWidget_C = true, WBP_LogHUD_C = true,
    CPP_WArningWidget_C = true}
-- These draw things where they are on screen (target markers, the aim crosshair, tooltips at the mouse), so they
-- must keep covering every monitor.
local KEEP = {MainIndicatorHUD_C = true, Test_Crosshair_C = true, SMW_ToolTipHolder_C = true}
local opts = {fov = false, hud = false, left = 0, width = 0}   -- all off until the bridge's tab turns them on
local off = {}          -- fixes that hit an error: name -> true
local moved = {}        -- addresses of widgets we put on the centre monitor -> their class
local wholeWindow = {}  -- addresses of other full-screen widgets -> their class: handled like SIZED
local gameInstance, statics, layout, pcKey, locked, lastNote
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

-- The local player's controller, fresh on every call. The game instance and the default objects last as long as
-- the game, so those are the only objects worth keeping.
local function controller()
    if not gameInstance then
        local g = FindFirstOf("GameInstance")
        if not valid(g) then return nil end
        gameInstance = g
        statics = StaticFindObject("/Script/Engine.Default__GameplayStatics")
        layout = StaticFindObject("/Script/UMG.Default__WidgetLayoutLibrary")
        if not valid(statics) or not valid(layout) then error("engine libraries not found") end
    end
    local c = statics:GetPlayerController(gameInstance, 0)
    if not valid(c) then pcKey = nil; return nil end
    local key = c:GetAddress()
    if key ~= pcKey then pcKey, locked, smooth = key, nil, nil end   -- a new controller has its own camera
    return c
end

local function viewport(c)
    local size = layout:GetViewportSize(c)
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
    local w, h = viewport(c)
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

local function slice(c)
    if opts.width > 0 then return opts.left, opts.width end
    local w, h = viewport(c)
    if w <= 0 or h <= 0 then return 0, 1 end
    local width = math.min(1, WIDE * h / w)
    return (1 - width) / 2, width
end

-- Puts a widget back over the whole window. `widget` is always a fresh object, never one we kept.
-- Widgets are only ever moved by size and position, never by anchors: SetAnchorsInViewport called from Lua leaves
-- the anchors at 0,0-0,0 (UE4SS 3.0 doesn't pass the struct), which pinned menus to the window's top-left corner.
local function restore(widget, c)
    local w, h = viewport(c)
    local scale = layout:GetViewportScale(c)
    if scale > 0 then
        widget:SetDesiredSizeInViewport({X = w / scale, Y = h / scale})
        widget:SetPositionInViewport({X = 0, Y = 0}, true)
    end
end

local logged = {}      -- widget classes already reported in UE4SS.log
local function report(class, text)
    if not logged[class] then logged[class] = true; print("[SB2HOTAS] Screen: " .. class .. " " .. text .. "\n") end
end

-- Full-screen widgets (HUD, menus, loot and elevator screens...) get the centre monitor's size and position.
-- Widgets the game places itself (markers, tooltips, anything at a spot of its own) keep it.
local function place(widget, c, left, width)
    local key = widget:GetAddress()
    local want = opts.hud and width < .999
    local class = widget:GetClass():GetFName():ToString()
    -- Once a widget is gone, a new one can get its address: remembered addresses only count for the same class.
    if moved[key] and moved[key] ~= class then moved[key] = nil end
    if wholeWindow[key] and wholeWindow[key] ~= class then wholeWindow[key] = nil end
    local mine = moved[key]
    if KEEP[class] then
        if mine then restore(widget, c); moved[key] = nil end
        return
    end
    if not (SIZED[class] or wholeWindow[key] or mine) then
        -- Not one of ours yet. Full-screen means anchored over the whole window, or pinned to its top-left corner at
        -- the window's height (how the game lays out some menus for one screen).
        local a = widget:GetAnchorsInViewport()
        local full = a.Minimum.X == 0 and a.Minimum.Y == 0 and a.Maximum.X == 1 and a.Maximum.Y == 1
        local corner = a.Minimum.X == 0 and a.Minimum.Y == 0 and a.Maximum.X == 0 and a.Maximum.Y == 0
        local ok, d = false, nil
        if corner then ok, d = pcall(function() return widget:GetDesiredSize() end) end
        local _, h = viewport(c)
        local scale = layout:GetViewportScale(c)
        local tall = ok and d ~= nil and scale > 0 and d.Y >= .9 * h / scale
        if not (full or tall) then
            if want then report(class .. "#", string.format("left in place (anchors %.2f,%.2f-%.2f,%.2f, its own size %s)",
                a.Minimum.X, a.Minimum.Y, a.Maximum.X, a.Maximum.Y, ok and d and string.format("%.0fx%.0f", d.X, d.Y) or "?")) end
            return
        end
        wholeWindow[key] = class     -- once sized, it is pinned to a corner: remember it was full-screen
    end
    if want then
        local w, h = viewport(c)
        local scale = layout:GetViewportScale(c)
        if scale <= 0 then return end
        report(class, "put on the centre monitor")
        widget:SetDesiredSizeInViewport({X = width * w / scale, Y = h / scale})
        widget:SetPositionInViewport({X = left * w, Y = 0}, true)
        moved[key] = class
    elseif mine then
        restore(widget, c)
        moved[key] = nil
    end
end

local function hud()
    if not opts.hud and next(moved) == nil then return end
    local c = controller()
    if not c then return end
    local left, width = slice(c)
    local seen = {}
    for _, w in ipairs(FindAllOf("UserWidget") or {}) do
        if valid(w) then
            local key = w:GetAddress()
            seen[key] = true
            if w:IsInViewport() or (moved[key] and not opts.hud) then place(w, c, left, width) end
        end
    end
    -- Widgets that no longer exist: forget them (never touch them), so a new widget at the same address starts clean.
    for key in pairs(moved) do if not seen[key] then moved[key] = nil end end
    for key in pairs(wholeWindow) do if not seen[key] then wholeWindow[key] = nil end end
end

-- The game is giving `widget` this size: if that is the whole window, it is one of its menus, even if not in SIZED.
local function learn(widget, c, size)
    local s = size:get()
    local w, h = viewport(c)
    local scale = layout:GetViewportScale(c)
    if scale <= 0 then return end
    local class = widget:GetClass():GetFName():ToString()
    if math.abs(s.X - w / scale) < 2 and math.abs(s.Y - h / scale) < 2 then
        wholeWindow[widget:GetAddress()] = class
        report(class .. "+", "is sized to the whole window by the game")
    elseif not SIZED[class] then
        report(class .. "~", string.format("is sized %.0fx%.0f by the game (window %.0fx%.0f)", s.X, s.Y, w / scale, h / scale))
    end
end

-- Everything here runs on the game thread, from the game's own ticks and calls: no LoopAsync. Lua is not safe on
-- two threads at once, and a background loop next to the per-frame flight hooks crashed the game now and then.

-- Called by the flight hook on every frame of the player's ship, so FOV effects stay smooth in flight.
function M.frame() guard("FOV", fov) end

-- Moving a widget calls the same engine functions main.lua hooks, so while Screen moves widgets itself, those
-- hooks must not come back in here.
local busy = false

-- Called by every ship and cockpit tick, the player's or not, and by the widget hooks: once a second, re-read the
-- bridge's choices, place the HUD and menus, and keep the FOV right outside the cockpit too.
-- Looking through every widget (FindAllOf) costs this game 50-90 ms, a hitch you can see, so it only happens when
-- something changed: the bridge's choices, the window's size, or a new map (main.lua calls M.rescan). Widgets that
-- appear or move in between reach M.widget through main.lua's hooks.
local scanned, rescan = nil, true
function M.rescan() rescan = true end
local function hudWhenChanged()
    local c = controller()
    if not c then return end
    local w, h = viewport(c)
    local now = string.format("%s %s %s %s %d %d", tostring(opts.fov), tostring(opts.hud), opts.left, opts.width, w, h)
    if not rescan and now == scanned then return end
    rescan, scanned = false, now
    hud()
end

local nextTick = 0
function M.tick()
    if busy then return end
    local now = os.clock()
    if now < nextTick then return end
    nextTick = now + 1
    busy = true
    pcall(readOptions)
    guard("HUD", hudWhenChanged)
    guard("FOV", fov)
    busy = false
end

-- Called just before the game sizes `widget` (main.lua hooks SetDesiredSizeInViewport; the size is only readable
-- before the call): a menu about to fill the whole window is remembered, and placed right after the call.
function M.sizing(widget, size)
    if busy or off.HUD or not opts.hud then return end
    local c = controller()
    if not c then return end
    local ok, err = pcall(learn, widget, c, size)
    if not ok then report("size", "of a widget unreadable: " .. tostring(err)) end
end

-- Called right after the game puts a widget on screen, sizes, moves or anchors it (main.lua hooks those calls), so
-- a menu lands on the centre monitor at once, even in the main menu where nothing else ticks. `widget` comes from
-- that hook: it is used now and never kept. `onlyMoved`: the call happens a lot (markers are moved every frame),
-- so only widgets Screen has placed itself are looked at, to put them back. `size`: the size the game just gave it.
function M.widget(widget, onlyMoved, size)
    if busy then return end
    M.tick()
    if off.HUD or (not opts.hud and next(moved) == nil) then return end
    if onlyMoved and not moved[widget:GetAddress()] then return end
    busy = true
    guard("HUD", function()
        if not valid(widget) then return end
        local c = controller()
        if not c then return end
        if size then pcall(learn, widget, c, size) end   -- a hint only: never worth switching the HUD fix off
        if not widget:IsInViewport() then return end
        local left, width = slice(c)
        place(widget, c, left, width)
    end)
    busy = false
end

function M.start() pcall(readOptions) end

return M
