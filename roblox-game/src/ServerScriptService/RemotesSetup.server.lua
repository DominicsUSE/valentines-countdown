local ReplicatedStorage = game:GetService("ReplicatedStorage")

local remotesFolder = Instance.new("Folder")
remotesFolder.Name = "Remotes"
remotesFolder.Parent = ReplicatedStorage

local remoteNames = {
	"LanternLit", -- server -> all clients: (litCount, totalCount)
	"PlayerCaught", -- server -> caught client: ()
	"PlayerRescued", -- server -> freed client: ()
	"GameStateChanged", -- server -> all clients: (bannerText)
	"SetSprinting", -- client -> server: (isSprinting)
}

for _, name in ipairs(remoteNames) do
	local remote = Instance.new("RemoteEvent")
	remote.Name = name
	remote.Parent = remotesFolder
end
