local DataStoreService = game:GetService("DataStoreService")
local ReplicatedStorage = game:GetService("ReplicatedStorage")

local GameConfig = require(ReplicatedStorage.Modules.GameConfig)

local store = DataStoreService:GetDataStore(GameConfig.DataStoreName)

local PlayerDataManager = {}

function PlayerDataManager.LoadEscapes(userId)
	local success, result = pcall(function()
		return store:GetAsync("Player_" .. userId)
	end)

	if success and typeof(result) == "table" and typeof(result.Escapes) == "number" then
		return result.Escapes
	end

	if not success then
		warn("[PlayerDataManager] Failed to load data for " .. userId .. ": " .. tostring(result))
	end

	return 0
end

function PlayerDataManager.SaveEscapes(userId, escapes)
	local success, err = pcall(function()
		store:SetAsync("Player_" .. userId, { Escapes = escapes })
	end)

	if not success then
		warn("[PlayerDataManager] Failed to save data for " .. userId .. ": " .. tostring(err))
	end
end

return PlayerDataManager
