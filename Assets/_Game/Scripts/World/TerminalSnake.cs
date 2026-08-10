using System.Collections.Generic;
using UnityEngine;

namespace Fields.World
{
    /// <summary>
    /// ASCII Snake game that runs inside the cheat terminal.
    /// Call Tick() on a timer; call HandleKey() for arrow input.
    /// Read the Frame property to get the current rendered string.
    /// </summary>
    public class TerminalSnake
    {
        const int W = 38;   // playfield width (inside walls)
        const int H = 13;   // playfield height (inside walls)

        enum Dir { Up, Down, Left, Right }

        readonly List<Vector2Int> _snake = new();
        Vector2Int _food;
        Dir _dir = Dir.Right;
        Dir _nextDir = Dir.Right;
        bool _alive = true;
        bool _started = false;
        int _score;

        System.Random _rng = new();

        public bool IsAlive => _alive;
        public bool HasStarted => _started;
        public int  Score => _score;

        public TerminalSnake()
        {
            // Start snake in the middle, length 3, heading right
            int cx = W / 2;
            int cy = H / 2;
            _snake.Add(new Vector2Int(cx,     cy));
            _snake.Add(new Vector2Int(cx - 1, cy));
            _snake.Add(new Vector2Int(cx - 2, cy));
            SpawnFood();
        }

        // Called from terminal Update() with a timer
        public void Tick()
        {
            if (!_alive || !_started) return;

            _dir = _nextDir;
            var head = _snake[0];
            Vector2Int newHead = _dir switch
            {
                Dir.Up    => new Vector2Int(head.x,     head.y - 1),
                Dir.Down  => new Vector2Int(head.x,     head.y + 1),
                Dir.Left  => new Vector2Int(head.x - 1, head.y),
                Dir.Right => new Vector2Int(head.x + 1, head.y),
                _         => head
            };

            // Wall collision
            if (newHead.x < 0 || newHead.x >= W || newHead.y < 0 || newHead.y >= H)
            {
                _alive = false;
                return;
            }

            // Self collision (skip tail — it will move)
            for (int i = 0; i < _snake.Count - 1; i++)
            {
                if (_snake[i] == newHead) { _alive = false; return; }
            }

            _snake.Insert(0, newHead);

            if (newHead == _food)
            {
                _score++;
                SpawnFood();
                // Don't remove tail — snake grows
            }
            else
            {
                _snake.RemoveAt(_snake.Count - 1);
            }
        }

        public void SetDir(KeyCode key)
        {
            if (!_started) _started = true;

            Dir desired = key switch
            {
                KeyCode.UpArrow    => Dir.Up,
                KeyCode.DownArrow  => Dir.Down,
                KeyCode.LeftArrow  => Dir.Left,
                KeyCode.RightArrow => Dir.Right,
                _ => _nextDir
            };

            // Prevent reversing
            if ((desired == Dir.Up    && _dir == Dir.Down)  ||
                (desired == Dir.Down  && _dir == Dir.Up)    ||
                (desired == Dir.Left  && _dir == Dir.Right) ||
                (desired == Dir.Right && _dir == Dir.Left)) return;

            _nextDir = desired;
        }

        public string Frame()
        {
            var sb = new System.Text.StringBuilder();
            // Top border
            sb.Append('+');
            for (int x = 0; x < W; x++) sb.Append('-');
            sb.AppendLine("+");

            // Grid
            var headSet = _snake.Count > 0 ? _snake[0] : new Vector2Int(-1, -1);
            var bodySet = new HashSet<Vector2Int>(_snake);

            for (int y = 0; y < H; y++)
            {
                sb.Append('|');
                for (int x = 0; x < W; x++)
                {
                    var p = new Vector2Int(x, y);
                    if (p == headSet)
                        sb.Append('@');
                    else if (bodySet.Contains(p))
                        sb.Append('o');
                    else if (p == _food)
                        sb.Append('*');
                    else
                        sb.Append(' ');
                }
                sb.AppendLine("|");
            }

            // Bottom border
            sb.Append('+');
            for (int x = 0; x < W; x++) sb.Append('-');
            sb.Append('+');

            return sb.ToString();
        }

        void SpawnFood()
        {
            var snakeSet = new HashSet<Vector2Int>(_snake);
            Vector2Int f;
            int tries = 0;
            do
            {
                f = new Vector2Int(_rng.Next(W), _rng.Next(H));
                tries++;
            } while (snakeSet.Contains(f) && tries < 200);
            _food = f;
        }
    }
}
