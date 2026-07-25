local Players = game:GetService("Players")
local ReplicatedStorage = game:GetService("ReplicatedStorage")
local UserInputService = game:GetService("UserInputService")
local TweenService = game:GetService("TweenService")

local player = Players.LocalPlayer
local playerGui = player:WaitForChild("PlayerGui")

local remotes = ReplicatedStorage:WaitForChild("Remotes")
local lanternLitRemote = remotes:WaitForChild("LanternLit")
local playerCaughtRemote = remotes:WaitForChild("PlayerCaught")
local playerRescuedRemote = remotes:WaitForChild("PlayerRescued")
local gameStateRemote = remotes:WaitForChild("GameStateChanged")
local setSprintingRemote = remotes:WaitForChild("SetSprinting")

local screenGui = Instance.new("ScreenGui")
screenGui.Name = "HollowHUD"
screenGui.ResetOnSpawn = false
screenGui.IgnoreGuiInset = true
screenGui.Parent = playerGui

-- Round banner
local banner = Instance.new("TextLabel")
banner.Size = UDim2.new(0, 520, 0, 40)
banner.Position = UDim2.new(0.5, -260, 0, 16)
banner.BackgroundColor3 = Color3.fromRGB(10, 8, 12)
banner.BackgroundTransparency = 0.25
banner.TextColor3 = Color3.fromRGB(230, 210, 190)
banner.Font = Enum.Font.Creepster
banner.TextSize = 22
banner.Text = "Welcome to Lantern Hollow"
banner.Parent = screenGui

local bannerCorner = Instance.new("UICorner")
bannerCorner.CornerRadius = UDim.new(0, 10)
bannerCorner.Parent = banner

gameStateRemote.OnClientEvent:Connect(function(text)
	banner.Text = text
end)

-- Lantern progress
local lanternLabel = Instance.new("TextLabel")
lanternLabel.Size = UDim2.new(0, 220, 0, 30)
lanternLabel.Position = UDim2.new(0, 16, 0, 16)
lanternLabel.BackgroundColor3 = Color3.fromRGB(10, 8, 12)
lanternLabel.BackgroundTransparency = 0.25
lanternLabel.TextColor3 = Color3.fromRGB(255, 200, 90)
lanternLabel.Font = Enum.Font.FredokaOne
lanternLabel.TextSize = 18
lanternLabel.Text = "Lanterns: 0 / 0"
lanternLabel.Parent = screenGui

local lanternCorner = Instance.new("UICorner")
lanternCorner.CornerRadius = UDim.new(0, 8)
lanternCorner.Parent = lanternLabel

lanternLitRemote.OnClientEvent:Connect(function(lit, total)
	lanternLabel.Text = "Lanterns: " .. lit .. " / " .. total
end)

-- Courage bar
local courageBack = Instance.new("Frame")
courageBack.Size = UDim2.new(0, 220, 0, 22)
courageBack.Position = UDim2.new(0, 16, 0, 52)
courageBack.BackgroundColor3 = Color3.fromRGB(30, 25, 30)
courageBack.Parent = screenGui

local courageBackCorner = Instance.new("UICorner")
courageBackCorner.CornerRadius = UDim.new(0, 8)
courageBackCorner.Parent = courageBack

local courageFill = Instance.new("Frame")
courageFill.Size = UDim2.new(1, 0, 1, 0)
courageFill.BackgroundColor3 = Color3.fromRGB(255, 140, 60)
courageFill.Parent = courageBack

local courageFillCorner = Instance.new("UICorner")
courageFillCorner.CornerRadius = UDim.new(0, 8)
courageFillCorner.Parent = courageFill

local courageLabel = Instance.new("TextLabel")
courageLabel.BackgroundTransparency = 1
courageLabel.Size = UDim2.new(1, 0, 1, 0)
courageLabel.Font = Enum.Font.FredokaOne
courageLabel.TextSize = 14
courageLabel.TextColor3 = Color3.new(1, 1, 1)
courageLabel.Text = "Courage"
courageLabel.Parent = courageBack

local function refreshCourage()
	local courage = player:GetAttribute("Courage") or 100
	courageFill.Size = UDim2.new(math.clamp(courage / 100, 0, 1), 0, 1, 0)
end

player:GetAttributeChangedSignal("Courage"):Connect(refreshCourage)
refreshCourage()

-- Jumpscare overlay (shown only to the player who got caught)
local jumpscareOverlay = Instance.new("Frame")
jumpscareOverlay.Size = UDim2.new(1, 0, 1, 0)
jumpscareOverlay.BackgroundColor3 = Color3.fromRGB(120, 0, 0)
jumpscareOverlay.BackgroundTransparency = 1
jumpscareOverlay.ZIndex = 10
jumpscareOverlay.Parent = screenGui

local function playJumpscare()
	jumpscareOverlay.BackgroundTransparency = 1
	local flashIn = TweenService:Create(jumpscareOverlay, TweenInfo.new(0.08), { BackgroundTransparency = 0.15 })
	flashIn:Play()
	flashIn.Completed:Wait()

	TweenService:Create(jumpscareOverlay, TweenInfo.new(1.2), { BackgroundTransparency = 1 }):Play()

	local camera = workspace.CurrentCamera
	if camera then
		local originalFov = camera.FieldOfView
		camera.FieldOfView = originalFov + 15
		task.delay(0.6, function()
			if camera then
				TweenService:Create(camera, TweenInfo.new(0.6), { FieldOfView = originalFov }):Play()
			end
		end)
	end
end

playerCaughtRemote.OnClientEvent:Connect(function()
	banner.Text = "CAUGHT! Wait for a friend to free you..."
	playJumpscare()
end)

playerRescuedRemote.OnClientEvent:Connect(function()
	banner.Text = "You're free! Keep exploring the Hollow!"
end)

-- Sprint controls (keyboard Shift + on-screen button for mobile)
local function setSprint(isSprinting)
	setSprintingRemote:FireServer(isSprinting)
end

UserInputService.InputBegan:Connect(function(input, gameProcessed)
	if gameProcessed then
		return
	end
	if input.KeyCode == Enum.KeyCode.LeftShift or input.KeyCode == Enum.KeyCode.RightShift then
		setSprint(true)
	end
end)

UserInputService.InputEnded:Connect(function(input)
	if input.KeyCode == Enum.KeyCode.LeftShift or input.KeyCode == Enum.KeyCode.RightShift then
		setSprint(false)
	end
end)

if UserInputService.TouchEnabled then
	local sprintButton = Instance.new("TextButton")
	sprintButton.Size = UDim2.new(0, 90, 0, 90)
	sprintButton.Position = UDim2.new(1, -110, 1, -110)
	sprintButton.BackgroundColor3 = Color3.fromRGB(255, 140, 60)
	sprintButton.Text = "RUN"
	sprintButton.Font = Enum.Font.FredokaOne
	sprintButton.TextSize = 20
	sprintButton.TextColor3 = Color3.new(1, 1, 1)
	sprintButton.Parent = screenGui

	local sprintButtonCorner = Instance.new("UICorner")
	sprintButtonCorner.CornerRadius = UDim.new(1, 0)
	sprintButtonCorner.Parent = sprintButton

	sprintButton.MouseButton1Down:Connect(function()
		setSprint(true)
	end)
	sprintButton.MouseButton1Up:Connect(function()
		setSprint(false)
	end)
end
