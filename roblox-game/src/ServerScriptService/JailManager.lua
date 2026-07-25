local CollectionService = game:GetService("CollectionService")
local Debris = game:GetService("Debris")
local ReplicatedStorage = game:GetService("ReplicatedStorage")

local GameConfig = require(ReplicatedStorage.Modules.GameConfig)

local remotes = ReplicatedStorage:WaitForChild("Remotes")
local playerCaughtRemote = remotes:WaitForChild("PlayerCaught")
local playerRescuedRemote = remotes:WaitForChild("PlayerRescued")

local JailManager = {}

local jailedPlayers = {}
local jailZoneCFrame = CFrame.new()

local function findJailZone()
	local zones = CollectionService:GetTagged("JailZone")
	return zones[1]
end

function JailManager.Init()
	local zone = findJailZone()
	if zone then
		jailZoneCFrame = zone.CFrame
	end

	for _, lever in ipairs(CollectionService:GetTagged("JailLever")) do
		local prompt = lever:FindFirstChildOfClass("ProximityPrompt")
		if prompt then
			prompt.Triggered:Connect(function()
				JailManager.ReleaseAll()
			end)
		end
	end
end

function JailManager.SendToJail(player)
	if jailedPlayers[player] then
		return false
	end

	local character = player.Character
	if not character then
		return false
	end

	local humanoid = character:FindFirstChildOfClass("Humanoid")
	local rootPart = character:FindFirstChild("HumanoidRootPart")
	if not humanoid or not rootPart then
		return false
	end

	jailedPlayers[player] = true
	player:SetAttribute("Jailed", true)

	local offset = Vector3.new(math.random(-6, 6), 0, math.random(-6, 6))
	rootPart.CFrame = jailZoneCFrame * CFrame.new(offset + Vector3.new(0, 3, 0))
	humanoid.WalkSpeed = 0
	humanoid.JumpPower = 0

	playerCaughtRemote:FireClient(player)

	local catchSound = Instance.new("Sound")
	catchSound.SoundId = "rbxasset://sounds/impact_water.mp3"
	catchSound.Volume = 1
	catchSound.Parent = rootPart
	catchSound:Play()
	Debris:AddItem(catchSound, 3)

	return true
end

function JailManager.ReleaseAll()
	local releasedAny = false

	for player in pairs(jailedPlayers) do
		local character = player.Character
		if character then
			local humanoid = character:FindFirstChildOfClass("Humanoid")
			if humanoid then
				humanoid.WalkSpeed = GameConfig.BaseWalkSpeed
				humanoid.JumpPower = 50
			end
		end
		player:SetAttribute("Jailed", false)
		playerRescuedRemote:FireClient(player)
		releasedAny = true
	end

	jailedPlayers = {}
	return releasedAny
end

function JailManager.CountJailed()
	local count = 0
	for _ in pairs(jailedPlayers) do
		count += 1
	end
	return count
end

function JailManager.ClearForNewRound()
	jailedPlayers = {}
end

return JailManager
