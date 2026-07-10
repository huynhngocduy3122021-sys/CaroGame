using UnityEngine;

// Class chứa kết quả trả về sau mỗi nước đi
public class WinResult 
{
    public bool isWin;
    public bool isDraw;
    public GameManager.PlayerType winner;
    public GameManager.Orientation orientation;
    public Vector2Int centerPos;
}

public static class MatchRules 
{
    // Hàm chính để kiểm tra trạng thái ván cờ
    public static WinResult CheckMatchStatus(GameManager.PlayerType[,] board, int x, int y, int moveCount, int maxMoves)
    {
        GameManager.PlayerType player = board[x, y];
        if (player == GameManager.PlayerType.None) return new WinResult { isWin = false };

        if (CheckLine(board, x, y, 1, 0, player)) 
            return CreateWinResult(board, x, y, 1, 0, player, GameManager.Orientation.Horizontal);
        
        if (CheckLine(board, x, y, 0, 1, player)) 
            return CreateWinResult(board, x, y, 0, 1, player, GameManager.Orientation.Vertical);
        
        if (CheckLine(board, x, y, 1, 1, player)) 
            return CreateWinResult(board, x, y, 1, 1, player, GameManager.Orientation.DiagonalA);
        
        if (CheckLine(board, x, y, 1, -1, player)) 
            return CreateWinResult(board, x, y, 1, -1, player, GameManager.Orientation.DiagonalB);

        if (moveCount >= maxMoves)
            return new WinResult { isDraw = true, isWin = false };

        return new WinResult { isWin = false, isDraw = false };
    }

    private static bool CheckLine(GameManager.PlayerType[,] board, int x, int y, int dirX, int dirY, GameManager.PlayerType player)
    {
        int count = 1;
        count += CountInDirection(board, x, y, dirX, dirY, player);
        count += CountInDirection(board, x, y, -dirX, -dirY, player);
        return count >= 5;
    }

    private static int CountInDirection(GameManager.PlayerType[,] board, int x, int y, int dirX, int dirY, GameManager.PlayerType player)
    {
        int count = 0;
        int checkX = x + dirX;
        int checkY = y + dirY;
        int width = board.GetLength(0);
        int height = board.GetLength(1);

        while (checkX >= 0 && checkX < width && checkY >= 0 && checkY < height && board[checkX, checkY] == player)
        {
            count++;
            checkX += dirX;
            checkY += dirY;
        }
        return count;
    }

    private static WinResult CreateWinResult(GameManager.PlayerType[,] board, int x, int y, int dirX, int dirY, GameManager.PlayerType player, GameManager.Orientation orientation)
    {
        return new WinResult
        {
            isWin = true,
            isDraw = false,
            winner = player,
            orientation = orientation,
            centerPos = GetWinCenter(board, x, y, dirX, dirY, player)
        };
    }

    private static Vector2Int GetWinCenter(GameManager.PlayerType[,] board, int x, int y, int dirX, int dirY, GameManager.PlayerType player)
    {
        int minX = x, minY = y;
        int maxX = x, maxY = y;
        int width = board.GetLength(0);
        int height = board.GetLength(1);

        int checkX = x + dirX;
        int checkY = y + dirY;
        while (checkX >= 0 && checkX < width && checkY >= 0 && checkY < height && board[checkX, checkY] == player)
        {
            maxX = checkX; maxY = checkY;
            checkX += dirX; checkY += dirY;
        }

        checkX = x - dirX;
        checkY = y - dirY;
        while (checkX >= 0 && checkX < width && checkY >= 0 && checkY < height && board[checkX, checkY] == player)
        {
            minX = checkX; minY = checkY;
            checkX -= dirX; checkY -= dirY;
        }

        return new Vector2Int((minX + maxX) / 2, (minY + maxY) / 2);
    }
}