-- The bridge uses the same folder (%LOCALAPPDATA%\SB2HOTAS), so no machine-specific paths are needed.
local base = os.getenv("LOCALAPPDATA") or ((os.getenv("USERPROFILE") or ".") .. "\\AppData\\Local")
local dir = base .. "\\SB2HOTAS\\"
return { state = dir .. "state.txt", status = dir .. "status.txt", screen = dir .. "screen.txt" }
