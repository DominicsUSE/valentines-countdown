local CollectionService = game:GetService("CollectionService")
local Lighting = game:GetService("Lighting")
local ReplicatedStorage = game:GetService("ReplicatedStorage")

local GameConfig = require(ReplicatedStorage.Modules.GameConfig)

-- Atmosphere
Lighting.Ambient = Color3.fromRGB(25, 22, 30)
Lighting.OutdoorAmbient = Color3.fromRGB(30, 25, 35)
Lighting.Brightness = 1
Lighting.ClockTime = 0
Lighting.FogColor = Color3.fromRGB(35, 25, 40)
Lighting.FogStart = 40
Lighting.FogEnd = 220

local atmosphere = Instance.new("Atmosphere")
atmosphere.Density = 0.4
atmosphere.Offset = 0.25
atmosphere.Color = Color3.fromRGB(60, 50, 70)
atmosphere.Decay = Color3.fromRGB(20, 15, 25)
atmosphere.Glare = 0
atmosphere.Haze = 2
atmosphere.Parent = Lighting

local structuresFolder = Instance.new("Folder")
structuresFolder.Name = "Structures"
structuresFolder.Parent = workspace

local decorationsFolder = Instance.new("Folder")
decorationsFolder.Name = "Decorations"
decorationsFolder.Parent = workspace

local pathsFolder = Instance.new("Folder")
pathsFolder.Name = "Paths"
pathsFolder.Parent = workspace

local GROUND_Y = 3

--------------------------------------------------------------------
-- Shared helpers
--------------------------------------------------------------------

local function createGround(name, center, radius, color, material, parent)
	local ground = Instance.new("Part")
	ground.Name = name
	ground.Shape = Enum.PartType.Cylinder
	ground.Size = Vector3.new(GROUND_Y * 2, radius * 2, radius * 2)
	ground.CFrame = CFrame.new(center) * CFrame.Angles(0, 0, math.rad(90))
	ground.Anchored = true
	ground.CanCollide = true
	ground.Color = color
	ground.Material = material
	ground.Parent = parent
	return ground
end

local function createPath(fromPos, toPos)
	local direction = toPos - fromPos
	local length = direction.Magnitude
	local midpoint = fromPos + direction / 2

	local plank = Instance.new("Part")
	plank.Name = "Path"
	plank.Size = Vector3.new(12, 1, length)
	plank.Color = Color3.fromRGB(45, 40, 50)
	plank.Material = Enum.Material.Cobblestone
	plank.Anchored = true
	plank.CanCollide = true
	plank.CFrame = CFrame.lookAt(midpoint, midpoint + direction)
	plank.Parent = pathsFolder

	return plank
end

local function createTorch(position)
	local pole = Instance.new("Part")
	pole.Name = "TorchPole"
	pole.Size = Vector3.new(0.5, 5, 0.5)
	pole.Color = Color3.fromRGB(35, 30, 30)
	pole.Material = Enum.Material.Wood
	pole.Anchored = true
	pole.CanCollide = false
	pole.CFrame = CFrame.new(position + Vector3.new(0, 2.5, 0))
	pole.Parent = decorationsFolder

	local flame = Instance.new("Part")
	flame.Name = "TorchFlame"
	flame.Shape = Enum.PartType.Ball
	flame.Size = Vector3.new(1, 1.4, 1)
	flame.Color = Color3.fromRGB(255, 140, 40)
	flame.Material = Enum.Material.Neon
	flame.Anchored = true
	flame.CanCollide = false
	flame.CFrame = CFrame.new(position + Vector3.new(0, 5.2, 0))
	flame.Parent = decorationsFolder

	local fire = Instance.new("Fire")
	fire.Size = 4
	fire.Heat = 6
	fire.Color = Color3.fromRGB(255, 140, 40)
	fire.SecondaryColor = Color3.fromRGB(120, 20, 10)
	fire.Parent = flame

	local light = Instance.new("PointLight")
	light.Color = Color3.fromRGB(255, 140, 40)
	light.Range = 18
	light.Brightness = 1.5
	light.Parent = flame
end

local function createLanternPost(position, name)
	local model = Instance.new("Model")
	model.Name = name or "LanternPost"

	local pole = Instance.new("Part")
	pole.Name = "LanternPole"
	pole.Size = Vector3.new(0.6, 6, 0.6)
	pole.Color = Color3.fromRGB(40, 35, 30)
	pole.Material = Enum.Material.Metal
	pole.Anchored = true
	pole.CanCollide = true
	pole.CFrame = CFrame.new(position + Vector3.new(0, 3, 0))
	pole.Parent = model

	local glow = Instance.new("Part")
	glow.Name = "LanternGlow"
	glow.Shape = Enum.PartType.Ball
	glow.Size = Vector3.new(1.6, 1.6, 1.6)
	glow.Color = Color3.fromRGB(40, 40, 45)
	glow.Material = Enum.Material.SmoothPlastic
	glow.Anchored = true
	glow.CanCollide = false
	glow.CFrame = CFrame.new(position + Vector3.new(0, 6.2, 0))
	glow.Parent = model

	local light = Instance.new("PointLight")
	light.Range = 16
	light.Brightness = 2
	light.Color = Color3.fromRGB(255, 200, 90)
	light.Enabled = false
	light.Parent = glow

	model.PrimaryPart = pole
	model.Parent = decorationsFolder

	CollectionService:AddTag(model, "LanternPost")

	return model
end

local function createPatrolPoint(position)
	local point = Instance.new("Part")
	point.Name = "PatrolPoint"
	point.Size = Vector3.new(1, 1, 1)
	point.Transparency = 1
	point.CanCollide = false
	point.Anchored = true
	point.CFrame = CFrame.new(position)
	point.Parent = decorationsFolder
	CollectionService:AddTag(point, "PatrolPoint")
end

--------------------------------------------------------------------
-- Lobby (safe waiting area, far from the haunted map)
--------------------------------------------------------------------

local LOBBY_CENTER = Vector3.new(0, 0, 0)
local LOBBY_RADIUS = 40

createGround("LobbyGround", LOBBY_CENTER, LOBBY_RADIUS, Color3.fromRGB(70, 60, 80), Enum.Material.Cobblestone, structuresFolder)

local lobbySpawnPart = Instance.new("Part")
lobbySpawnPart.Name = "LobbySpawnMarker"
lobbySpawnPart.Size = Vector3.new(6, 1, 6)
lobbySpawnPart.Transparency = 1
lobbySpawnPart.CanCollide = false
lobbySpawnPart.Anchored = true
lobbySpawnPart.CFrame = CFrame.new(LOBBY_CENTER + Vector3.new(0, GROUND_Y + 0.5, 0))
lobbySpawnPart.Parent = structuresFolder
CollectionService:AddTag(lobbySpawnPart, "LobbySpawn")

local realSpawn = Instance.new("SpawnLocation")
realSpawn.Name = "RealSpawn"
realSpawn.Size = Vector3.new(8, 1, 8)
realSpawn.Transparency = 1
realSpawn.Anchored = true
realSpawn.CanCollide = true
realSpawn.Neutral = true
realSpawn.CFrame = CFrame.new(LOBBY_CENTER + Vector3.new(0, GROUND_Y + 0.5, 0))
realSpawn.Parent = structuresFolder

local welcomeSign = Instance.new("Part")
welcomeSign.Name = "WelcomeSign"
welcomeSign.Size = Vector3.new(10, 6, 0.5)
welcomeSign.Color = Color3.fromRGB(20, 16, 24)
welcomeSign.Material = Enum.Material.Wood
welcomeSign.Anchored = true
welcomeSign.CanCollide = false
welcomeSign.CFrame = CFrame.new(LOBBY_CENTER + Vector3.new(0, GROUND_Y + 4, -20))
welcomeSign.Parent = decorationsFolder

local welcomeGui = Instance.new("SurfaceGui")
welcomeGui.Face = Enum.NormalId.Front
welcomeGui.Parent = welcomeSign

local welcomeLabel = Instance.new("TextLabel")
welcomeLabel.Size = UDim2.new(1, 0, 1, 0)
welcomeLabel.BackgroundTransparency = 1
welcomeLabel.Font = Enum.Font.Creepster
welcomeLabel.TextColor3 = Color3.fromRGB(255, 200, 90)
welcomeLabel.TextScaled = true
welcomeLabel.Text = "LANTERN HOLLOW\nLight every lantern and escape The Ringmaster.\nHold Shift to run. Don't run out of Courage!"
welcomeLabel.Parent = welcomeGui

--------------------------------------------------------------------
-- The Hollow (haunted carnival map, offset far from the lobby)
--------------------------------------------------------------------

local HOLLOW_ORIGIN = Vector3.new(1500, 0, 0)
local PLAZA_RADIUS = 36
local WING_DISTANCE = 110
local WING_RADIUS = 28

local plazaCenter = HOLLOW_ORIGIN
createGround("Plaza", plazaCenter, PLAZA_RADIUS, Color3.fromRGB(55, 48, 60), Enum.Material.Cobblestone, structuresFolder)

for i = 1, 4 do
	local angle = (i / 4) * math.pi * 2
	createPatrolPoint(plazaCenter + Vector3.new(math.cos(angle) * (PLAZA_RADIUS - 8), GROUND_Y + 3, math.sin(angle) * (PLAZA_RADIUS - 8)))
end

local WINGS = {
	{ Name = "ToyWing", AngleDeg = 0, Color = Color3.fromRGB(60, 30, 60), Material = Enum.Material.Slate, Lanterns = 2 },
	{ Name = "MirrorWing", AngleDeg = 90, Color = Color3.fromRGB(60, 60, 70), Material = Enum.Material.Marble, Lanterns = 2 },
	{ Name = "GreenhouseWing", AngleDeg = 180, Color = Color3.fromRGB(30, 45, 30), Material = Enum.Material.Ground, Lanterns = 1 },
	{ Name = "ClocktowerWing", AngleDeg = 270, Color = Color3.fromRGB(50, 40, 25), Material = Enum.Material.Metal, Lanterns = 1 },
}

for _, wing in ipairs(WINGS) do
	local wingCenter = plazaCenter + Vector3.new(
		math.cos(math.rad(wing.AngleDeg)) * WING_DISTANCE,
		0,
		math.sin(math.rad(wing.AngleDeg)) * WING_DISTANCE
	)

	createGround(wing.Name, wingCenter, WING_RADIUS, wing.Color, wing.Material, structuresFolder)

	local normalized = (wingCenter - plazaCenter).Unit
	local plazaEdge = plazaCenter + normalized * PLAZA_RADIUS
	local wingEdge = wingCenter - normalized * WING_RADIUS

	createPath(
		Vector3.new(plazaEdge.X, GROUND_Y + 0.5, plazaEdge.Z),
		Vector3.new(wingEdge.X, GROUND_Y + 0.5, wingEdge.Z)
	)

	createTorch(plazaEdge)
	createTorch(wingEdge)

	for i = 1, wing.Lanterns do
		local angle = math.random() * math.pi * 2
		local dist = math.random() * (WING_RADIUS - 8)
		local pos = wingCenter + Vector3.new(math.cos(angle) * dist, GROUND_Y, math.sin(angle) * dist)
		createLanternPost(pos, wing.Name .. "Lantern" .. i)
	end

	for i = 1, 3 do
		local angle = math.random() * math.pi * 2
		local dist = math.random() * (WING_RADIUS - 6)
		createPatrolPoint(wingCenter + Vector3.new(math.cos(angle) * dist, GROUND_Y + 3, math.sin(angle) * dist))
	end
end

-- Map spawn points (used when a round starts)
for i = 1, 4 do
	local angle = (i / 4) * math.pi * 2 + math.pi / 4
	local spawnPart = Instance.new("Part")
	spawnPart.Name = "MapSpawn"
	spawnPart.Size = Vector3.new(4, 1, 4)
	spawnPart.Transparency = 1
	spawnPart.CanCollide = false
	spawnPart.Anchored = true
	spawnPart.CFrame = CFrame.new(plazaCenter + Vector3.new(math.cos(angle) * 10, GROUND_Y + 0.5, math.sin(angle) * 10))
	spawnPart.Parent = structuresFolder
	CollectionService:AddTag(spawnPart, "MapSpawn")
end

-- Exit gate (locked until every lantern is lit)
local gate = Instance.new("Part")
gate.Name = "ExitGate"
gate.Size = Vector3.new(8, 10, 2)
gate.Color = Color3.fromRGB(90, 20, 20)
gate.Material = Enum.Material.Metal
gate.Anchored = true
gate.CanCollide = false
gate.CFrame = CFrame.new(plazaCenter + Vector3.new(0, GROUND_Y + 5, 0))
gate.Parent = structuresFolder
CollectionService:AddTag(gate, "ExitGate")

local gateLight = Instance.new("PointLight")
gateLight.Color = Color3.fromRGB(255, 40, 40)
gateLight.Range = 20
gateLight.Brightness = 2
gateLight.Parent = gate

local exitPrompt = Instance.new("ProximityPrompt")
exitPrompt.Name = "ExitPrompt"
exitPrompt.ActionText = "Escape the Hollow"
exitPrompt.ObjectText = "Locked Gate"
exitPrompt.HoldDuration = 1
exitPrompt.MaxActivationDistance = 10
exitPrompt.Enabled = false
exitPrompt.Parent = gate

--------------------------------------------------------------------
-- Jail (caught players are sent here until a friend frees them)
--------------------------------------------------------------------

local jailCenter = plazaCenter + Vector3.new(0, 0, -(PLAZA_RADIUS + 40))

createGround("JailGround", jailCenter, 20, Color3.fromRGB(30, 25, 30), Enum.Material.Slate, structuresFolder)

local jailZone = Instance.new("Part")
jailZone.Name = "JailZone"
jailZone.Size = Vector3.new(16, 1, 16)
jailZone.Transparency = 1
jailZone.CanCollide = false
jailZone.Anchored = true
jailZone.CFrame = CFrame.new(jailCenter + Vector3.new(0, GROUND_Y, 0))
jailZone.Parent = structuresFolder
CollectionService:AddTag(jailZone, "JailZone")

for i = 1, 10 do
	local angle = (i / 10) * math.pi * 2
	local bar = Instance.new("Part")
	bar.Name = "JailBar"
	bar.Size = Vector3.new(0.4, 6, 0.4)
	bar.Color = Color3.fromRGB(20, 20, 20)
	bar.Material = Enum.Material.Metal
	bar.Anchored = true
	bar.CanCollide = true
	bar.CFrame = CFrame.new(jailCenter + Vector3.new(math.cos(angle) * 14, GROUND_Y + 3, math.sin(angle) * 14))
	bar.Parent = decorationsFolder
end

local lever = Instance.new("Part")
lever.Name = "JailLever"
lever.Size = Vector3.new(1.5, 3, 1.5)
lever.Color = Color3.fromRGB(150, 30, 30)
lever.Material = Enum.Material.Metal
lever.Anchored = true
lever.CanCollide = true
lever.CFrame = CFrame.new(jailCenter + Vector3.new(0, GROUND_Y + 1.5, 16))
lever.Parent = structuresFolder
CollectionService:AddTag(lever, "JailLever")

local leverPrompt = Instance.new("ProximityPrompt")
leverPrompt.Name = "FreePrompt"
leverPrompt.ActionText = "Free Trapped Friends"
leverPrompt.ObjectText = "Rescue Lever"
leverPrompt.HoldDuration = GameConfig.JailFreeHoldSeconds
leverPrompt.MaxActivationDistance = 8
leverPrompt.Parent = lever

createPath(
	Vector3.new(plazaCenter.X, GROUND_Y + 0.5, plazaCenter.Z - PLAZA_RADIUS),
	Vector3.new(jailCenter.X, GROUND_Y + 0.5, jailCenter.Z + 20)
)
