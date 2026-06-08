using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class CaroAgent : Agent
{  private const int WIDTH = 30;
    private const int HEIGHT = 18;

    private int[,] board = new int[WIDTH + 1, HEIGHT + 1];

    public override void OnEpisodeBegin()
    {
        board = new int[WIDTH + 1, HEIGHT + 1];
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        for (int y = 0; y <= HEIGHT; y++)
        {
            for (int x = 0; x <= WIDTH; x++)
            {
                sensor.AddObservation(board[x, y]);
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int action = actions.DiscreteActions[0];

        int x = action % (WIDTH + 1);
        int y = action / (WIDTH + 1);

        if (board[x, y] != 0)
        {
            AddReward(-0.2f);
            EndEpisode();
            return;
        }

        board[x, y] = 1;

        if (CheckWin(x, y, 1))
        {
            AddReward(1f);
            EndEpisode();
            return;
        }

        AddReward(-0.01f);

        RandomOpponentMove();
    }

    private void RandomOpponentMove()
    {
        for (int i = 0; i < 1000; i++)
        {
            int x = Random.Range(0, WIDTH + 1);
            int y = Random.Range(0, HEIGHT + 1);

            if (board[x, y] == 0)
            {
                board[x, y] = -1;

                if (CheckWin(x, y, -1))
                {
                    AddReward(-1f);
                    EndEpisode();
                }

                return;
            }
        }

        EndEpisode();
    }

    private bool CheckWin(int x, int y, int player)
    {
        return CheckLine(x, y, 1, 0, player)
            || CheckLine(x, y, 0, 1, player)
            || CheckLine(x, y, 1, 1, player)
            || CheckLine(x, y, 1, -1, player);
    }

    private bool CheckLine(int x, int y, int dx, int dy, int player)
    {
        int count = 1;
        count += CountDirection(x, y, dx, dy, player);
        count += CountDirection(x, y, -dx, -dy, player);
        return count >= 5;
    }

    private int CountDirection(int x, int y, int dx, int dy, int player)
    {
        int count = 0;
        int cx = x + dx;
        int cy = y + dy;

        while (
            cx >= 0 && cx <= WIDTH &&
            cy >= 0 && cy <= HEIGHT &&
            board[cx, cy] == player
        )
        {
            count++;
            cx += dx;
            cy += dy;
        }

        return count;
    }
}

