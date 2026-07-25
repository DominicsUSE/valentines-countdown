local Players = game:GetService("Players")
local RunService = game:GetService("RunService")
local ReplicatedStorage = game:GetService("ReplicatedStorage")

local GameConfig = require(ReplicatedStorage.Modules.GameConfig)

local remotes = ReplicatedStorage:WaitForChild("Remotes")
local setSprintingRemote = remotes:WaitForChild("SetSprinting")

local sprintingPlayers = {}

setSprintingRemote.OnServerEvent:Connect(function(player, wantsToSprint)
	sprintingPlayers[player] = wantsToSprint and true or nil
end)

Players.PlayerRemoving:Connect(function(player)
	sprintingPlayers[player] = nil
end)

RunService.Heartbeat:Connect(function(dt)
	for _, player in ipairs(Players:GetPlayers()) do
		local character = player.Character
		local humanoid = character and character:FindFirstChildOfClass("Humanoid")
		if not humanoid then
			continue
		end

		if player:GetAttribute("Jailed") then
			humanoid.WalkSpeed = 0
			continue
		end

		local courage = player:GetAttribute("Courage") or 100
		local wantsSprint = sprintingPlayers[player] and courage > GameConfig.MinCourageToSprint

		if wantsSprint then
			courage = math.max(0, courage - GameConfig.CourageDrainPerSecond * dt)
			humanoid.WalkSpeed = GameConfig.SprintWalkSpeed
		else
			courage = math.min(100, courage + GameConfig.CourageRegenPerSecond * dt)
			humanoid.WalkSpeed = GameConfig.BaseWalkSpeed
		end

		player:SetAttribute("Courage", courage)
	end
end)
