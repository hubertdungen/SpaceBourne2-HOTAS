-- Frame the bridge writes to state.txt (protocol 2), space separated:
-- 2 seq time flight head pitch roll yaw throttle strafe lift x y z headYaw headPitch headRoll rate mask
-- Bit i of mask (pitch=0 ... lift=5) is set when the mod should drive that axis; the rest stay with the game.
-- Bit 6 means the throttle comes from the bridge's keyboard fallback, so the game's speed lock still applies.
local M = {}
local AXES = {"pitch","roll","yaw","throttle","strafe","lift"}
function M.parse(text, now)
    if type(text) ~= "string" or #text > 1024 then return nil end
    local v = {}
    for token in text:gmatch("%S+") do
        local n = tonumber(token)
        if not n or n ~= n or n == math.huge or n == -math.huge then return nil end
        v[#v+1] = n
    end
    if #v ~= 19 or v[1] ~= 2 or v[2] < 0 or v[2] % 1 ~= 0 then return nil end
    if now-v[3] > 2 or v[3]-now > 2 then return nil end
    if (v[4] ~= 0 and v[4] ~= 1) or (v[5] ~= 0 and v[5] ~= 1) then return nil end
    for i=6,11 do if math.abs(v[i]) > 1 then return nil end end
    for i=12,14 do if math.abs(v[i]) > 400 then return nil end end
    for i=15,17 do if math.abs(v[i]) > 720 then return nil end end
    if v[18] < 1 or v[18] > 120 then return nil end
    local mask = math.tointeger(v[19])
    if not mask or mask < 0 or mask > 127 then return nil end
    local has = {keyboardThrottle = mask & 64 ~= 0}
    for i,name in ipairs(AXES) do has[name] = mask & (1 << (i-1)) ~= 0 end
    return {seq=v[2], flight=v[4]==1, head=v[5]==1, has=has,
        pitch=v[6], roll=v[7], yaw=v[8], throttle=v[9], strafe=v[10], lift=v[11],
        x=v[12], y=v[13], z=v[14], headYaw=v[15], headPitch=v[16], headRoll=v[17], rate=v[18]}
end
function M.clamp(v,lo,hi) return math.max(lo,math.min(hi,v)) end
function M.watchdog()
    local last, elapsed = nil, 0
    return function(frame, dt)
        if not frame then last=nil; elapsed=0; return false end
        if frame.seq ~= last then last=frame.seq; elapsed=0
        else elapsed=elapsed+math.max(0,dt) end
        return elapsed < .25
    end
end
return M
