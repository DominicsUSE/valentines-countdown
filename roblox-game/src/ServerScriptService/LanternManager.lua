local CollectionService = game:GetService("CollectionService")
local ReplicatedStorage = game:GetService("ReplicatedStorage")

local remotes = ReplicatedStorage:WaitForChild("Remotes")
local lanternLitRemote = remotes:WaitForChild("LanternLit")

local LanternManager = {}

local litCount = 0
local totalCount = 0
local lanternEntries = {}

local function setLanternVisual(post, lit)
	local glow = post:FindFirstChild("LanternGlow")
	if glow then
		glow.Color = lit and Color3.fromRGB(255, 200, 90) or Color3.fromRGB(40, 40, 45)
		glow.Material = lit and Enum.Material.Neon or Enum.Material.SmoothPlastic
	end

	local light = post:FindFirstChild("PointLight", true)
	if light then
		light.Enabled = lit
	end
end

function LanternManager.Init()
	for _, post in ipairs(CollectionService:GetTagged("LanternPost")) do
		totalCount += 1
		post:SetAttribute("Lit", false)
		setLanternVisual(post, false)

		local prompt = Instance.new("ProximityPrompt")
		prompt.ActionText = "Light Lantern"
		prompt.ObjectText = "Old Lamp"
		prompt.HoldDuration = 1.5
		prompt.MaxActivationDistance = 8
		prompt.Parent = post:FindFirstChild("LanternGlow") or post

		prompt.Triggered:Connect(function()
			if post:GetAttribute("Lit") then
				return
			end

			post:SetAttribute("Lit", true)
			setLanternVisual(post, true)
			prompt.Enabled = false

			litCount += 1
			lanternLitRemote:FireAllClients(litCount, totalCount)
		end)

		table.insert(lanternEntries, { Post = post, Prompt = prompt })
	end
end

function LanternManager.AllLit()
	return totalCount > 0 and litCount >= totalCount
end

function LanternManager.GetProgress()
	return litCount, totalCount
end

function LanternManager.ResetAll()
	litCount = 0
	for _, entry in ipairs(lanternEntries) do
		entry.Post:SetAttribute("Lit", false)
		setLanternVisual(entry.Post, false)
		entry.Prompt.Enabled = true
	end
	lanternLitRemote:FireAllClients(0, totalCount)
end

return LanternManager
