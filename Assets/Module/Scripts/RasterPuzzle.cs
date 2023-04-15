using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SysRnd = System.Random;


public class RasterPuzzle
{
    private class AttemptCounter
    {
        private const int MaxAttempts = 1 << 6;
        private int _counter = MaxAttempts;

        public void Count()
        {
            if (_counter == 0)
                throw new AttemptsExceededException();

            _counter--;
        }

        public override string ToString()
        {
            return "[Failed Attempts:" + (MaxAttempts - _counter) + "/" + MaxAttempts + "]";
        }

        public class AttemptsExceededException : Exception
        {
            public AttemptsExceededException() : base("Exceeded allotted attempt count.") { }
        }
    }

    private const int MaxHorizontalDistance = 7;
    private const int MaxVerticalDistance = 12;
    private const int GridSize = MaxHorizontalDistance + MaxVerticalDistance + 1;

    private string _inputs = "";

    private SysRnd _random;

    public RasterShape LeftComponent { get; private set; }

    private enum CellState
    {
        OutOfBounds,
        Occupied,
        MustBeOccupied,
        Free,
    }

    private CellState[,] _grid = new CellState[GridSize, GridSize];
    private int[,] _indexGrid = new int[GridSize, GridSize];

    public static RasterPuzzle GeneratePuzzle(SysRnd random)
    {
        RasterPuzzle preset = new RasterPuzzle(random);
        RasterPuzzle puzzle = null;
        while (true)
        {
            try
            {
                AttemptCounter counter = new AttemptCounter();
                puzzle = preset.GenerateStep(random.Next(16, 20), 0, 0, counter);

                for (int i = 0; i < GridSize; i++)
                    for (int j = 0; j < GridSize; j++)
                        if (puzzle._grid[i, j] == CellState.MustBeOccupied)
                            puzzle._grid[i, j] = CellState.Free;

                List<string> solutions = new RasterPuzzle(puzzle, 0, "!").CalculateSolutions();
                if (solutions.Distinct().Count() == 1 && solutions.First().Substring(2) == puzzle._inputs)
                    break;

                Debug.Log("[Raster Prime] Puzzle generation attempt failed: Unclear solution.");
            }
            catch (AttemptCounter.AttemptsExceededException)
            {
                Debug.Log("[Raster Prime] Puzzle generation attempt failed: Exceeded attempt count.");
            }
        }
        return puzzle;
    }

    //Create first item
    private RasterPuzzle(SysRnd random)
    {
        _random = random;
        LeftComponent = PickRandom(RasterShape.GetAllShapes().ToList());
        Initialise();
    }

    //Create clone after new chunk
    private RasterPuzzle(RasterPuzzle parent, int minY, string inputs = "")
    {
        _random = parent._random;

        bool isSolving = inputs.Length != 0;

        LeftComponent = parent.LeftComponent;

        _inputs = !isSolving ? parent._inputs + ' ' : inputs;
        _grid = parent._grid.Clone() as CellState[,];

        _indexGrid = parent._indexGrid.Clone() as int[,];

        StartChunk(minY, isSolving);
    }

    //Create clone after placement
    private RasterPuzzle(RasterPuzzle parent, bool isLeftPiece, int v, int h, string inputs = "")
    {
        _random = parent._random;

        bool isSolving = inputs.Length != 0;

        LeftComponent = parent.LeftComponent;

        _inputs = !isSolving ? parent._inputs + (isLeftPiece ? 'L' : 'R') : inputs;
        _grid = parent._grid.Clone() as CellState[,];

        _indexGrid = parent._indexGrid.Clone() as int[,];

        RasterShape shape = LeftComponent;
        if (!isLeftPiece)
            shape = shape.Counterpart();

        for (int i = 0; i < GridSize; i++)
            for (int j = 0; j < GridSize; j++)
                if (_grid[i, j] == CellState.MustBeOccupied)
                    _grid[i, j] = CellState.OutOfBounds;

        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                if (shape.OccupiedTiles[i, j])
                {
                    //Will also override any faulty states that are created below
                    _grid[v + i, h + j] = !isSolving ? CellState.Occupied : CellState.OutOfBounds;

                    _indexGrid[v + i, h + j] = _inputs.Length;

                    //Top right, bottom left, top left, bottom right
                    int[] vCoords = { v + i - 1, v + i + 1, v + i, v + i };
                    int[] hCoords = { h + j, h + j, h + j - 1, h + j + 1 };
                    bool[] boundaryCondition = { vCoords[0] >= 0, vCoords[1] < GridSize, hCoords[2] >= 0, hCoords[3] < GridSize };

                    for (int k = 0; k < 4; k++)
                    {
                        if (boundaryCondition[k] && _grid[vCoords[k], hCoords[k]].EqualsAny(!isSolving ? CellState.Free : CellState.Occupied, CellState.MustBeOccupied))
                            _grid[vCoords[k], hCoords[k]] = CellState.MustBeOccupied;
                    }
                }
    }

    private int CountMustOccupyTiles()
    {
        int requiredOccupiedTiles = 0;
        for (int i = 0; i < GridSize; i++)
            for (int j = 0; j < GridSize; j++)
                if (_grid[i, j] == CellState.MustBeOccupied)
                    requiredOccupiedTiles++;

        return requiredOccupiedTiles;
    }

    private List<RasterPuzzle> GetAllBranches(bool isLeftPiece, bool isSolving = false)
    {
        int requiredOccupiedTiles = 1;
        if (isSolving)
        {
            requiredOccupiedTiles = CountMustOccupyTiles();

            if (requiredOccupiedTiles == 0)
            {
                RasterPuzzle branch;
                int y = 0;
                do
                {
                    branch = new RasterPuzzle(this, y, _inputs + ' ');
                    y++;

                    requiredOccupiedTiles = branch.CountMustOccupyTiles();

                    if (y > GridSize * 2)
                    {
                        return new List<RasterPuzzle>();
                    }
                }
                while (requiredOccupiedTiles == 0);
                return new List<RasterPuzzle> { branch };
            }
        }

        RasterShape shape = LeftComponent;
        if (!isLeftPiece)
            shape = shape.Counterpart();

        List<RasterPuzzle> branches = new List<RasterPuzzle>();

        for (int i = -2; i < GridSize - 2; i++)
            for (int j = -2; j < GridSize - 2; j++)
            {
                int occupiesMust = 0;
                bool occupiesBlock = false;
                for (int a = 0; a < 3; a++)
                    for (int b = 0; b < 3; b++)
                    {
                        if (!shape.OccupiedTiles[a, b])
                            continue;

                        if (a + i < 0 || b + j < 0)
                        {
                            occupiesBlock = true;
                            break;
                        }

                        occupiesBlock |= _grid[i + a, j + b].EqualsAny(CellState.OutOfBounds, isSolving ? CellState.Free : CellState.Occupied);
                        if (_grid[i + a, j + b] == CellState.MustBeOccupied)
                            occupiesMust++;
                    }

                if (occupiesBlock || occupiesMust < requiredOccupiedTiles)
                    continue;

                branches.Add(new RasterPuzzle(this, isLeftPiece, i, j, isSolving ? _inputs + (isLeftPiece ? 'L' : 'R') : ""));
            }

        return branches;
    }

    private RasterPuzzle GenerateStep(int targetDepth, int chunkSize, int targetY, AttemptCounter counter)
    {
        int maxY = FindMaxY();
        bool validChunk = chunkSize > 1 && maxY > targetY;

        if (targetDepth == 0)
            if (validChunk)
                return this;
            else
            {
                counter.Count();
                return null;
            }

        bool newChunk = validChunk && _random.NextDouble() < 0.015625f;

        Queue<RasterPuzzle> queue = new Queue<RasterPuzzle>();
        if (newChunk)
            for (int i = GridSize - 1 - MaxVerticalDistance; i < GridSize - 1 + MaxVerticalDistance; i++)
                queue.Enqueue(new RasterPuzzle(this, i));

        if (queue.Count == 0)
            queue = new Queue<RasterPuzzle>(Shuffle(GetAllBranches(true).Concat(GetAllBranches(false)).ToList()));

        RasterPuzzle returnValue = null;

        if (queue.Count == 0)
        {
            counter.Count();
            return null;
        }

        while (returnValue == null && queue.Count > 0)
            returnValue = queue.Dequeue().GenerateStep(targetDepth - 1, newChunk ? 0 : chunkSize + 1, newChunk ? maxY + 1 : targetY, counter);

        //If no successes it will return null
        return returnValue;
    }

    private List<string> CalculateSolutions()
    {
        bool safe = true;
        for (int i = 0; i < GridSize; i++)
            for (int j = 0; j < GridSize; j++)
                if (_grid[i, j].EqualsAny(CellState.Occupied, CellState.MustBeOccupied))
                {
                    safe = false;
                    break;
                }

        if (safe)
            return new List<string> { _inputs };

        Queue<RasterPuzzle> queue = new Queue<RasterPuzzle>(GetAllBranches(true, true).Concat(GetAllBranches(false, true)));

        int requiredOccupiedTiles = CountMustOccupyTiles();

        if (requiredOccupiedTiles == 0)
        {
            RasterPuzzle branch;
            int y = 0;
            do
            {
                branch = new RasterPuzzle(this, y, _inputs + ' ');
                y++;

                requiredOccupiedTiles = branch.CountMustOccupyTiles();

                if (y > GridSize * 2)
                {
                    throw new Exception("What happened here?");
                }
            }
            while (requiredOccupiedTiles == 0);
            return branch.CalculateSolutions();
        }

        List<string> solutions = new List<string>();

        while (queue.Count > 0)
            solutions.AddRange(queue.Dequeue().CalculateSolutions());


        //If no successes it will return an empty list
        return solutions;
    }

    private void Initialise()
    {
        for (int i = 0; i < GridSize; i++)
            for (int j = 0; j < GridSize; j++)
            {
                int x = j - i;
                int y = j + i;

                if (y < GridSize - 1 - MaxVerticalDistance || y > GridSize - 1 + MaxVerticalDistance)
                {
                    _grid[i, j] = CellState.OutOfBounds;
                    continue;
                }

                if (x < -MaxHorizontalDistance || x > MaxHorizontalDistance)
                {
                    _grid[i, j] = CellState.OutOfBounds;
                    continue;
                }

                if (y == GridSize - 1 - MaxVerticalDistance)
                {
                    _grid[i, j] = CellState.MustBeOccupied;
                    continue;
                }

                _grid[i, j] = CellState.Free;
            }
    }

    private int StartChunk(int yPosition, bool isSolving = false)
    {
        int mustCount = 0;
        for (int i = 0; i < GridSize; i++)
            for (int j = 0; j < GridSize; j++)
            {
                if (_grid[i, j].EqualsAny(CellState.OutOfBounds, !isSolving ? CellState.Occupied : CellState.Free))
                    continue;

                if (_grid[i, j] == CellState.MustBeOccupied)
                {
                    _grid[i, j] = CellState.OutOfBounds;
                    continue;
                }

                int y = j + i;

                if (y < yPosition)
                {
                    _grid[i, j] = CellState.OutOfBounds;
                    continue;
                }

                if (y == yPosition)
                {
                    mustCount++;
                    _grid[i, j] = CellState.MustBeOccupied;
                    continue;
                }
            }

        return mustCount;
    }

    private int FindMaxY()
    {
        int maxY = 0;
        for (int i = 0; i < GridSize; i++)
            for (int j = 0; j < GridSize; j++)
            {
                int y = j + i;
                if (_grid[i, j] == CellState.Occupied && y > maxY)
                    maxY = y;
            }

        return maxY;
    }

    private List<MeshRenderer>[] _cellGroups;
    public IEnumerator InstantiateAllTiles(MeshRenderer reference)
    {
        Color color2 = RasterPrimeScript.ActiveColor;
        Color color1 = new Color(color2.r, color2.g, color2.b, 0);

        _cellGroups = Enumerable.Range(0, _inputs.Length).Select(_ => new List<MeshRenderer>()).ToArray();

        reference.enabled = true;
        reference.material.color = color1;

        int freeRowCount = GridSize - 1 + MaxVerticalDistance - FindMaxY();
        float offset = -(GridSize - 1 - freeRowCount / 2f) / 2f;

        for (int i = 0; i < GridSize; i++)
            for (int j = 0; j < GridSize; j++)
                if (_indexGrid[i, j] > 0)
                {
                    MeshRenderer copy = GameObject.Instantiate(reference, reference.transform.parent);
                    copy.transform.localPosition = new Vector3(j + offset, i + offset);
                    _cellGroups[_indexGrid[i, j] - 1].Add(copy);
                }

        reference.enabled = false;

        //Animating the reveal

        float t = 0;

        while (t < 1)
        {
            t += Time.deltaTime / 0.5f;
            foreach (List<MeshRenderer> renderers in _cellGroups)
                foreach (MeshRenderer renderer in renderers)
                    renderer.material.color = Color.Lerp(color1, color2, Mathf.Min(t, 1));

            yield return null;
        }
    }

    public IEnumerator SolveAnimationInitial(KMAudio audio, Transform transform)
    {
        Color color1 = RasterPrimeScript.InactiveColor;

        float t = 0;
        for (int i = 0; i < _inputs.Length; i++)
        {
            if (_inputs[i] == ' ')
                continue;
            audio.PlaySoundAtTransform("SolveClick", transform);
            foreach (MeshRenderer renderer in _cellGroups[i])
                renderer.material.color = color1;
            while (t < 1)
            {
                t += Time.deltaTime / 0.25f;
                yield return null;
            }
            t %= 1;
        }
    }

    public IEnumerator SolveAnimation()
    {
        Color color1 = RasterPrimeScript.InactiveColor;
        Color color2 = RasterPrimeScript.ActiveColor;

        float t = 0;
        while (true)
        {
            for (int i = 0; i < _inputs.Length + 1; i++)
            {
                while (t < 1)
                {
                    t += Time.deltaTime / 0.25f;
                    if (i < _inputs.Length)
                        foreach (MeshRenderer renderer in _cellGroups[i])
                            renderer.material.color = Color.Lerp(color1, color2, Mathf.Min(t, 1));
                    if (i > 0)
                        foreach (MeshRenderer renderer in _cellGroups[i - 1])
                            renderer.material.color = Color.Lerp(color2, color1, Mathf.Min(t, 1));
                    yield return null;
                }
                t %= 1;

                if (i >= _inputs.Length || _inputs[i] == ' ')
                    yield return new WaitForSeconds(0.25f);
            }
            yield return new WaitForSeconds(0.25f);
        }
    }

    public string GetSolution()
    {
        return _inputs;
    }

    //Due to inaccessibility of UnityEngine.Random
    private List<T> Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int swapIdx = _random.Next(i, list.Count);
            T temp = list[swapIdx];
            list[swapIdx] = list[i];
            list[i] = temp;
        }
        return list;
    }

    //Due to inaccessibility of UnityEngine.Random
    private T PickRandom<T>(List<T> list)
    {
        int index = _random.Next(0, list.Count);
        return list[index];
    }

    public override string ToString()
    {
        string gridstring = "";
        for (int i = 0; i < GridSize; i++)
        {
            if (i != 0)
                gridstring += '/';
            for (int j = 0; j < GridSize; j++)
                gridstring += _grid[i, j] == CellState.Occupied ? '#' : '-';
        }
        return gridstring;
    }
}
