using UnityEngine;

/// <summary>Các mức độ khó dùng cho chế độ chơi với máy.</summary>
public enum CaroDifficulty
{
    Normal,
    Hard
}

/// <summary>Thông số tìm kiếm của một mức độ khó trên thiết bị hiện tại.</summary>
public struct CaroDifficultySettings
{
    public int Depth;
    public int TimeLimitMs;
    public int CandidateRadius;
    public int MaxCandidates;

    /// <summary>Chọn ngân sách thời gian riêng cho PC và thiết bị di động.</summary>
    public static CaroDifficultySettings For(CaroDifficulty difficulty)
    {
        bool mobile = Application.isMobilePlatform;
        if (difficulty == CaroDifficulty.Hard)
        {
            return new CaroDifficultySettings
            {
                Depth = 4,
                TimeLimitMs = mobile ? 180 : 350,
                CandidateRadius = 2,
                MaxCandidates = mobile ? 20 : 28
            };
        }

        return new CaroDifficultySettings
        {
            Depth = 3,
            TimeLimitMs = mobile ? 80 : 150,
            CandidateRadius = 2,
            MaxCandidates = mobile ? 16 : 24
        };
    }
}
