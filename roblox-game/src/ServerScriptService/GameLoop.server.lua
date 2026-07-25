local Players = game:GetService("Players")
local CollectionService = game:GetService("CollectionService")
local ReplicatedStorage = game:GetService("ReplicatedStorage")
local ServerScriptService = game:GetService("ServerScriptService")

local GameConfig = require(ReplicatedStorage.Modules.GameConfig)
local LanternManager = require(ServerScriptService.LanternManager)
local JailManager = require(ServerScriptService.JailManager)
local MonsterAI = require(ServerScriptService.MonsterAI)

local remotes = ReplicatedStorage:WaitForChild("Remotes")
local gameStateRemote = remotes:WaitForChild("GameStateChanged")

-- Give WorldBuilder.server.lua (a sibling script with no yields) time to
-- finish tagging every structure before we go looking for them.
task.wait(1)

LanternManager.Init()
JailManager.Init()

local function getLobbySpawn()
	return CollectionService:GetTagged("LobbySpawn")[1]
end

local function getMapSpawns()
	return CollectionService:GetTagged("MapSpawn")
end

local function getExitGate()
	return CollectionService:GetTagged("ExitGate")[1]
end

local function teleportPlayer(player, cframe)
	local character = player.Character
	local rootPart = character and character:FindFirstChild("HumanoidRootPart")
	if rootPart then
		rootPart.CFrame = cframe + Vector3.new(0, 3, 0)
	end
end

local function broadcastState(text)
	gameStateRemote:FireAllClients(text)
end

local function awardEscape(player)
	local leaderstats = player:FindFirstChild("leaderstats")
	local escapes = leaderstats and leaderstats:FindFirstChild("Escapes")
	if escapes then
		escapes.Value += 1
	end
end

local exitGate = getExitGate()
local exitPrompt = exitGate and exitGate:FindFirstChildOfClass("ProximityPrompt")

if exitPrompt then
	exitPrompt.Enabled = false
	exitPrompt.Triggered:Connect(function(player)
		if not exitPrompt.Enabled or player:GetAttribute("Jailed") or player:GetAttribute("Escaped") then
			return
		end

		player:SetAttribute("Escaped", true)
		-- Award immediately (not at round end) so a player who escapes and
		-- then disconnects still keeps the credit for it.
		awardEscape(player)

		local lobbySpawn = getLobbySpawn()
		if lobbySpawn then
			teleportPlayer(player, lobbySpawn.CFrame)
		end
	end)
end

-- Win/lose checks only ever look at the players who were actually
-- teleported into this specific round (`participants`), not everyone
-- connected to the server. Otherwise a player who joins mid-round and is
-- waiting in the lobby for the next round would never have Escaped = true,
-- permanently blocking the round from ever resolving as a win.
local function allEscaped(participants)
	if #participants == 0 then
		return false
	end
	for _, player in ipairs(participants) do
		if player.Parent and not player:GetAttribute("Escaped") then
			return false
		end
	end
	return true
end

local function allJailedOrEscaped(participants)
	if #participants == 0 then
		return false
	end
	for _, player in ipairs(participants) do
		if player.Parent and not player:GetAttribute("Escaped") and not player:GetAttribute("Jailed") then
			return false
		end
	end
	return true
end

local function resetPlayerForRound(player)
	player:SetAttribute("Jailed", false)
	player:SetAttribute("Escaped", false)
	player:SetAttribute("Courage", 100)
end

local function runIntermission()
	broadcastState("Waiting for players...")

	while #Players:GetPlayers() < GameConfig.MinPlayersToStart do
		task.wait(1)
	end

	for seconds = GameConfig.IntermissionSeconds, 1, -1 do
		broadcastState("Next round starts in " .. seconds .. "...")
		task.wait(1)
	end
end

local function startRound()
	LanternManager.ResetAll()
	JailManager.ClearForNewRound()

	local roundParticipants = {}
	local mapSpawns = getMapSpawns()
	local spawnIndex = 0
	for _, player in ipairs(Players:GetPlayers()) do
		resetPlayerForRound(player)
		if player.Character and #mapSpawns > 0 then
			spawnIndex += 1
			local spawnPoint = mapSpawns[((spawnIndex - 1) % #mapSpawns) + 1]
			teleportPlayer(player, spawnPoint.CFrame)
			table.insert(roundParticipants, player)
		end
	end

	local _, totalLanterns = LanternManager.GetProgress()
	broadcastState("Find and light all " .. totalLanterns .. " lanterns!")
	task.wait(GameConfig.RoundWarmupSeconds)

	MonsterAI.Start()
	broadcastState("The Ringmaster is awake. Run!")

	local startTime = os.clock()
	local result = "Timeout"

	while os.clock() - startTime < GameConfig.RoundTimeLimitSeconds do
		if #Players:GetPlayers() == 0 then
			result = "Empty"
			break
		end

		if LanternManager.AllLit() and exitPrompt and not exitPrompt.Enabled then
			exitPrompt.Enabled = true
			broadcastState("The gate is open! Get out!")
		end

		if allEscaped(roundParticipants) then
			result = "Win"
			break
		end

		if allJailedOrEscaped(roundParticipants) then
			result = "Lose"
			break
		end

		task.wait(1)
	end

	MonsterAI.Stop()
	if exitPrompt then
		exitPrompt.Enabled = false
	end

	if result == "Win" then
		broadcastState("Everyone escaped! The Hollow falls quiet... for now.")
	elseif result == "Lose" then
		broadcastState("The Ringmaster caught everyone... Try again!")
	elseif result == "Empty" then
		-- No one left to announce anything to.
	else
		broadcastState("Time's up! The Hollow resets...")
	end

	task.wait(3)

	JailManager.ReleaseAll()
	local lobbySpawn = getLobbySpawn()
	for _, player in ipairs(Players:GetPlayers()) do
		if lobbySpawn then
			teleportPlayer(player, lobbySpawn.CFrame)
		end
	end
end

task.spawn(function()
	while true do
		runIntermission()
		startRound()
	end
end)
