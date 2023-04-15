using System.Collections.Generic;
using System.Linq;

public class RasterShape
{
    public bool[,] OccupiedTiles { get; private set; }
    private bool _usable;

    public RasterShape(bool[,] occupiedTiles)
    {
        _usable = true;

        //warning: gets stuck with all false

        bool noShift = false;
        for (int i = 0; i < 3; i++)
            noShift |= occupiedTiles[2, i];

        _usable &= noShift;

        noShift = false;
        for (int i = 0; i < 3; i++)
            noShift |= occupiedTiles[i, 2];

        _usable &= noShift;

        OccupiedTiles = occupiedTiles;
    }

    public void Simplify()
    {
        //warning: gets stuck with all false

        while (true)
        {
            bool noShift = false;
            for (int i = 0; i < 3; i++)
                noShift |= OccupiedTiles[2, i];

            if (noShift)
                break;

            for (int i = 2; i > 0; i--)
                for (int j = 0; j < 3; j++)
                    OccupiedTiles[i, j] = OccupiedTiles[i - 1, j];
            for (int i = 0; i < 3; i++)
                OccupiedTiles[0, i] = false;
        }

        while (true)
        {
            bool noShift = false;
            for (int i = 0; i < 3; i++)
                noShift |= OccupiedTiles[i, 2];

            if (noShift)
                break;

            for (int i = 2; i > 0; i--)
                for (int j = 0; j < 3; j++)
                    OccupiedTiles[j, i] = OccupiedTiles[j, i - 1];
            for (int i = 0; i < 3; i++)
                OccupiedTiles[i, 0] = false;
        }

        _usable = true;
    }

    public bool Matches(RasterShape other)
    {
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                if (OccupiedTiles[i, j] != other.OccupiedTiles[i, j])
                    return false;
        return true;
    }

    private RasterShape PartialCounterpart()
    {
        bool[,] counterpart = new bool[3, 3];
        RasterShape counterpartShape;

        //horizontal asymmetry
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                counterpart[i, j] = OccupiedTiles[j, i];

        counterpartShape = new RasterShape(counterpart);
        counterpartShape.Simplify();
        if (!Matches(counterpartShape))
            return counterpartShape;

        //vertical asymmetry
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                counterpart[i, 2 - j] = OccupiedTiles[j, 2 - i];

        counterpartShape = new RasterShape(counterpart);
        counterpartShape.Simplify();
        if (!Matches(counterpartShape))
            return counterpartShape;

        //rotational asymmetry
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                counterpart[i, 2 - j] = OccupiedTiles[j, i];

        counterpartShape = new RasterShape(counterpart);
        counterpartShape.Simplify();
        if (!Matches(counterpartShape))
            return counterpartShape;

        return null;
    }

    public RasterShape Counterpart()
    {
        bool[,] counterpart = new bool[3, 3];
        RasterShape counterpartShape;

        counterpartShape = PartialCounterpart();
        if (counterpartShape != null)
            return counterpartShape;

        //perfect symmetry (last resort)
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                counterpart[i, j] = !OccupiedTiles[i, j];

        counterpartShape = new RasterShape(counterpart);
        counterpartShape.Simplify();
        if (counterpartShape.PartialCounterpart() != null)
            return null;

        return counterpartShape;
    }

    private static IEnumerable<RasterShape> _allShapes = null;
    public static IEnumerable<RasterShape> GetAllShapes()
    {
        int lowerBound = 2, upperBound = 4;

        if (_allShapes != null)
            return _allShapes;

        List<RasterShape> possibleShapes = new List<RasterShape>();

        for (int i = 0; i < 1 << (3 * 3); i++)
        {
            int value = i;
            int total = 0;
            bool[,] grid = new bool[3, 3];
            for (int j = 0; j < 3; j++)
                for (int k = 0; k < 3; k++)
                {
                    grid[j, k] = (value & 1) != 0;
                    total += value & 1;
                    value >>= 1;
                }

            if (total < lowerBound || total > upperBound)
                continue;

            RasterShape shape = new RasterShape(grid);

            if (!shape._usable)
                continue;
            if (shape.Counterpart() == null)
                continue;

            possibleShapes.Add(shape);
        }

        _allShapes = possibleShapes.Concat(possibleShapes.Select(x => x.Counterpart()).Where(x => !possibleShapes.Any(y => y.Matches(x))));
        return _allShapes;
    }

    public override string ToString()
    {
        string text = "";
        for (int i = 0; i < 3; i++)
        {
            if (i != 0)
                text += '/';
            for (int j = 0; j < 3; j++)
                text += OccupiedTiles[i, j] ? '#' : '-';
        }
        return text;
    }
}

