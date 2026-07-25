local Players = game:GetService("Players")
local CollectionService = game:GetService("CollectionService")
local ServerScriptService = game:GetService("ServerScriptService")
local ReplicatedStorage = game:GetService("ReplicatedStorage")

local GameConfig = require(ReplicatedStorage.Modules.GameConfig)
local PlayerDataManager = require(ServerScriptService.PlayerDataManager)

local function getLobbySpawn()
	local tagged = CollectionService:GetTagged("LobbySpawn")
	return tagged[1]
end

local function buildLeaderstats(player, escapes)
	local leaderstats = Instance.new("Folder")
	leaderstats.Name = "leaderstats"

	local escapesValue = Instance.new("IntValue")
	escapesValue.Name = "Escapes"
	escapesValue.Value = escapes
	escapesValue.Parent = leaderstats

	leaderstats.Parent = player
end

Players.PlayerAdded:Connect(function(player)
	local escapes = PlayerDataManager.LoadEscapes(player.UserId)
	buildLeaderstats(player, escapes)

	player:SetAttribute("Courage", 100)
	player:SetAttribute("Jailed", false)
	player:SetAttribute("Escaped", false)

	player.CharacterAdded:Connect(function(character)
		local humanoid = character:WaitForChild("Humanoid")
		humanoid.WalkSpeed = GameConfig.BaseWalkSpeed

		local rootPart = character:WaitForChild("HumanoidRootPart")
		local lobbySpawn = getLobbySpawn()
		if lobbySpawn then
			rootPart.CFrame = lobbySpawn.CFrame + Vector3.new(0, 3, 0)
		end
	end)
end)

local function saveOnLeave(player)
	local leaderstats = player:FindFirstChild("leaderstats")
	local escapes = leaderstats and leaderstats:FindFirstChild("Escapes")
	if escapes then
		PlayerDataManager.SaveEscapes(player.UserId, escapes.Value)
	end
end

Players.PlayerRemoving:Connect(saveOnLeave)

game:BindToClose(function()
	for _, player in ipairs(Players:GetPlayers()) do
		saveOnLeave(player)
	end
end)
