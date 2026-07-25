local GameConfig = {}

GameConfig.DataStoreName = "LanternHollow_v1"

GameConfig.MinPlayersToStart = 1
GameConfig.IntermissionSeconds = 12
GameConfig.RoundWarmupSeconds = 5
GameConfig.RoundTimeLimitSeconds = 300

GameConfig.BaseWalkSpeed = 16
GameConfig.SprintWalkSpeed = 24
GameConfig.CourageDrainPerSecond = 16
GameConfig.CourageRegenPerSecond = 6
GameConfig.MinCourageToSprint = 12

GameConfig.MonsterPatrolSpeed = 10
GameConfig.MonsterChaseSpeed = 15
GameConfig.MonsterDetectionRadius = 40
GameConfig.MonsterLoseInterestRadius = 65
GameConfig.MonsterCatchRadius = 4.5
GameConfig.MonsterRepathSeconds = 0.5
GameConfig.MonsterTickSeconds = 0.2
GameConfig.MonsterPatrolArriveRadius = 6

GameConfig.JailFreeHoldSeconds = 3

return GameConfig
