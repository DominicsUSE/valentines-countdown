local Players = game:GetService("Players")
local CollectionService = game:GetService("CollectionService")
local ReplicatedStorage = game:GetService("ReplicatedStorage")
local ServerScriptService = game:GetService("ServerScriptService")

local GameConfig = require(ReplicatedStorage.Modules.GameConfig)
local JailManager = require(ServerScriptService.JailManager)

local MonsterAI = {}

local monsterModel, humanoid, rootPart
local running = false
local currentChaseTarget

local function buildMonster()
	local model = Instance.new("Model")
	model.Name = "TheRingmaster"

	local torso = Instance.new("Part")
	torso.Name = "HumanoidRootPart"
	torso.Size = Vector3.new(2.4, 3.6, 1.4)
	torso.Color = Color3.fromRGB(18, 14, 22)
	torso.Material = Enum.Material.Slate
	torso.CanCollide = true
	torso.Parent = model

	local head = Instance.new("Part")
	head.Name = "Head"
	head.Shape = Enum.PartType.Ball
	head.Size = Vector3.new(1.6, 1.6, 1.6)
	head.Color = Color3.fromRGB(235, 225, 210)
	head.Material = Enum.Material.SmoothPlastic
	head.CanCollide = false
	head.CFrame = torso.CFrame * CFrame.new(0, 2.6, 0)
	head.Parent = model

	local headWeld = Instance.new("WeldConstraint")
	headWeld.Part0 = torso
	headWeld.Part1 = head
	headWeld.Parent = head

	local hat = Instance.new("Part")
	hat.Name = "Hat"
	hat.Shape = Enum.PartType.Cylinder
	hat.Size = Vector3.new(1, 1.5, 1.5)
	hat.Color = Color3.fromRGB(110, 12, 20)
	hat.Material = Enum.Material.SmoothPlastic
	hat.CanCollide = false
	hat.CFrame = head.CFrame * CFrame.new(0, 1, 0) * CFrame.Angles(0, 0, math.rad(90))
	hat.Parent = model

	local hatWeld = Instance.new("WeldConstraint")
	hatWeld.Part0 = torso
	hatWeld.Part1 = hat
	hatWeld.Parent = hat

	for _, side in ipairs({ -1, 1 }) do
		local eye = Instance.new("Part")
		eye.Name = "Eye"
		eye.Shape = Enum.PartType.Ball
		eye.Size = Vector3.new(0.3, 0.3, 0.3)
		eye.Color = Color3.fromRGB(255, 25, 25)
		eye.Material = Enum.Material.Neon
		eye.CanCollide = false
		eye.CFrame = head.CFrame * CFrame.new(0.4 * side, 0, -0.75)
		eye.Parent = model

		local eyeWeld = Instance.new("WeldConstraint")
		eyeWeld.Part0 = torso
		eyeWeld.Part1 = eye
		eyeWeld.Parent = eye

		local light = Instance.new("PointLight")
		light.Color = Color3.fromRGB(255, 30, 30)
		light.Range = 16
		light.Brightness = 2
		light.Parent = eye
	end

	local hum = Instance.new("Humanoid")
	hum.WalkSpeed = GameConfig.MonsterPatrolSpeed
	hum.JumpPower = 0
	hum.HipHeight = 0
	hum.MaxHealth = 1000000
	hum.Health = 1000000
	hum.Parent = model

	model.PrimaryPart = torso

	return model, hum, torso
end

local function getPatrolPoints()
	return CollectionService:GetTagged("PatrolPoint")
end

local function findNearestFreePlayer()
	local bestPlayer, bestDist

	for _, player in ipairs(Players:GetPlayers()) do
		if player:GetAttribute("Jailed") or player:GetAttribute("Escaped") then
			continue
		end

		local character = player.Character
		local hrp = character and character:FindFirstChild("HumanoidRootPart")
		if hrp and rootPart then
			local dist = (hrp.Position - rootPart.Position).Magnitude
			if not bestDist or dist < bestDist then
				bestDist = dist
				bestPlayer = player
			end
		end
	end

	return bestPlayer, bestDist
end

local function aiLoop()
	local nextRepathTime = 0

	while running do
		task.wait(0.2)

		if not rootPart or not rootPart.Parent then
			break
		end

		local nearestPlayer, dist = findNearestFreePlayer()

		if currentChaseTarget and (not dist or dist > GameConfig.MonsterLoseInterestRadius) then
			currentChaseTarget = nil
		elseif nearestPlayer and dist and dist <= GameConfig.MonsterDetectionRadius then
			currentChaseTarget = nearestPlayer
		end

		if os.clock() < nextRepathTime then
			continue
		end
		nextRepathTime = os.clock() + GameConfig.MonsterRepathSeconds

		if currentChaseTarget and currentChaseTarget.Character then
			humanoid.WalkSpeed = GameConfig.MonsterChaseSpeed
			local hrp = currentChaseTarget.Character:FindFirstChild("HumanoidRootPart")
			if hrp then
				humanoid:MoveTo(hrp.Position)

				if (hrp.Position - rootPart.Position).Magnitude <= GameConfig.MonsterCatchRadius then
					JailManager.SendToJail(currentChaseTarget)
					currentChaseTarget = nil
				end
			end
		else
			humanoid.WalkSpeed = GameConfig.MonsterPatrolSpeed
			local patrolPoints = getPatrolPoints()
			if #patrolPoints > 0 then
				local point = patrolPoints[math.random(1, #patrolPoints)]
				humanoid:MoveTo(point.Position)
			end
		end
	end
end

function MonsterAI.Start()
	if running then
		return
	end
	running = true

	if not monsterModel then
		monsterModel, humanoid, rootPart = buildMonster()
	end

	local patrolPoints = getPatrolPoints()
	if #patrolPoints > 0 then
		rootPart.CFrame = CFrame.new(patrolPoints[math.random(1, #patrolPoints)].Position)
	end

	currentChaseTarget = nil
	monsterModel.Parent = workspace

	task.spawn(aiLoop)
end

function MonsterAI.Stop()
	running = false
	currentChaseTarget = nil
	if monsterModel then
		monsterModel.Parent = nil
	end
end

return MonsterAI
