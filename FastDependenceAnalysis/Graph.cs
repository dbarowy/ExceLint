﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Excel = Microsoft.Office.Interop.Excel;
using ExprOpt = Microsoft.FSharp.Core.FSharpOption<AST.Expression>;

namespace FastDependenceAnalysis
{
    public class Graphs
    {
        private readonly Graph[] _worksheet_graphs;
        private Dictionary<string, int> _worksheet_names_indices;

        public Graphs(Excel.Application a, Excel.Workbook wb)
        {
            _worksheet_names_indices = new Dictionary<string, int>();
            _worksheet_graphs = new Graph[wb.Worksheets.Count];
            int i = 0;
            foreach (Excel.Worksheet w in wb.Worksheets)
            {
                _worksheet_graphs[i] = new Graph(a, w);
                _worksheet_names_indices.Add(_worksheet_graphs[i].Worksheet, i);
                i++;
            }
        }

        public bool isFormula(AST.Address addr)
        {
            if (_worksheet_names_indices.ContainsKey(addr.WorksheetName))
            {
                return _worksheet_graphs[_worksheet_names_indices[addr.WorksheetName]].isFormula(addr);
            }

            return false;
        }

        public string getFormulaAtAddress(AST.Address addr)
        {
            if (_worksheet_names_indices.ContainsKey(addr.WorksheetName))
            {
                return _worksheet_graphs[_worksheet_names_indices[addr.WorksheetName]].getFormulaAtAddress(addr);
            }

            return null;
        }

        public Graph[] Worksheets
        {
            get { return _worksheet_graphs; }
        }

        public int NumFormulas
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _worksheet_graphs.Length; i++)
                {
                    n += _worksheet_graphs[i].NumFormulas;
                }

                return n;
            }
        }

        public int NumCells
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _worksheet_graphs.Length; i++)
                {
                    n += _worksheet_graphs[i].NumCells;
                }

                return n;
            }
        }

        public long TimeMarshalingMilliseconds
        {
            get
            {
                long n = 0;
                for (int i = 0; i < _worksheet_graphs.Length; i++)
                {
                    n += _worksheet_graphs[i].TimeMarshalingMilliseconds;
                }

                return n;
            }
        }

        public long TimeParsingMilliseconds
        {
            get
            {
                long n = 0;
                for (int i = 0; i < _worksheet_graphs.Length; i++)
                {
                    n += _worksheet_graphs[i].TimeParsingMilliseconds;
                }

                return n;
            }
        }

        public long TimeDependenceAnalysisMilliseconds
        {
            get
            {
                long n = 0;
                for (int i = 0; i < _worksheet_graphs.Length; i++)
                {
                    n += _worksheet_graphs[i].TimeDependenceAnalysisMilliseconds;
                }

                return n;
            }
        }

        public Dictionary<AST.Address, string> Formulas
        {
            get
            {
                Dictionary<AST.Address,string> d = new Dictionary<AST.Address, string>();
                for (int i = 0; i < _worksheet_graphs.Length; i++)
                {
                    var wd = _worksheet_graphs[i].Formulas;
                    foreach (var kvp in wd)
                    {
                        d.Add(kvp.Key, kvp.Value);
                    }
                }

                return d;
            }
        }
    }

    public class Graph : IDisposable
    {
        private readonly string _wsname;
        private readonly string _wbname;
        private readonly string _path;
        private readonly string[][] _formulaTable;
        private readonly object[][] _valueTable;
        private readonly Dictionary<Tuple<int, int>, List<Reference>> _referenceTable;

        // bounding boxes (using Excel's native R1C1 coordinate system)
        // used range
        private readonly int _used_range_top;        // 1-based top y coordinate
        private readonly int _used_range_bottom;     // 1-based bottom y coordinate
        private readonly int _used_range_left;       // 1-based left-hand x coordinate
        private readonly int _used_range_right;      // 1-based right-hand x coordinate
        private readonly int _used_range_width;
        private readonly int _used_range_height;

        // formulas
        private readonly int _formula_box_top;
        private readonly int _formula_box_bottom;
        private readonly int _formula_box_left;
        private readonly int _formula_box_right;

        // data
        private readonly int _value_box_top;
        private readonly int _value_box_bottom;
        private readonly int _value_box_left;
        private readonly int _value_box_right;

        // stats
        private readonly int _num_formulas;
        private readonly int _num_values;
        private readonly long _time_ms_marshaling;
        private readonly long _time_ms_parsing;
        private readonly long _time_ms_dep_analysis;

        // caches
        private Dictionary<AST.Address, string> _cache_formulas;
        private Dictionary<AST.Address, string> _cache_values;
        private AST.Address[] _cache_allCellsIncludingBlanks;
        private AST.Address[] _cache_allCells;

        public string Worksheet
        {
            get { return _wsname; }
        }
        public string Workbook
        {
            get { return _wbname; }
        }

        public string Path
        {
            get { return _path; }
        }

        public Dictionary<AST.Address,string> Formulas
        {
            get {
                if (_cache_formulas == null)
                {
                    Dictionary<AST.Address, string> d = new Dictionary<AST.Address, string>();
                    for (int row = 0; row < _formulaTable.Length; row++)
                    {
                        for (int col = 0; col < _formulaTable[row].Length; col++)
                        {
                            if (_formulaTable[row][col] != null)
                            {
                                var addr = FormulaReferenceToAddress(row, col);
                                d.Add(addr, _formulaTable[row][col]);
                            }
                        }
                    }

                    _cache_formulas = d;
                    return d;
                }
                else
                {
                    return _cache_formulas;
                }
            }
        }

        public int NumFormulas
        {
            get { return _num_formulas; }
        }

        // This is a count of non-empty cells,
        // which includes data and formulas
        public int NumCells
        {
            get { return _num_values; }
        }

        public long TimeMarshalingMilliseconds
        {
            get { return _time_ms_marshaling; }
        }

        public long TimeParsingMilliseconds
        {
            get { return _time_ms_parsing; }
        }

        public long TimeDependenceAnalysisMilliseconds
        {
            get { return _time_ms_dep_analysis; }
        }

        public Dictionary<AST.Address, string> Values
        {
            get
            {
                if (_cache_values == null)
                {
                    var d = new Dictionary<AST.Address, string>();
                    for (int row = 0; row < _valueTable.Length; row++)
                    {
                        for (int col = 0; col < _valueTable[row].Length; col++)
                        {
                            var addr = ValueReferenceToAddress(row, col);
                            d.Add(addr, Convert.ToString(_valueTable[row][col]));
                        }
                    }

                    _cache_values = d;
                    return d;
                }
                else
                {
                    return _cache_values;
                }
            }
        }

        public bool isOffSheet(AST.Address addr)
        {
            return addr.Path != _path || addr.WorkbookName != _wbname || addr.WorksheetName != _wsname;
        }

        public static string WorksheetName(Excel.Worksheet w)
        {
            return w.Name;
        }

        public static string WorkbookName(Excel.Worksheet w)
        {
            return ((Excel.Workbook)w.Parent).Name;
        }

        public static string WorkbookPath(Excel.Worksheet w)
        {
            var pthtmp = ((Excel.Workbook)w.Parent).Path;
            if (pthtmp.EndsWith(@"\"))
            {
                return pthtmp;
            }
            else
            {
                return pthtmp + @"\";
            }
        }

        public Graph(Excel.Application a, Excel.Worksheet w)
        {
            // allocate stopwatches
            var sw = new System.Diagnostics.Stopwatch();    // general purpose
            var psw = new System.Diagnostics.Stopwatch();   // for parsing

            #region MARSHALING
            sw.Start();

            // get names once
            _wsname = WorksheetName(w);
            _wbname = WorkbookName(w);
            _path = WorkbookPath(w);

            // get used range
            Excel.Range urng = w.UsedRange;

            // get dimensions
            _used_range_left = urng.Column;
            _used_range_right = urng.Columns.Count + _used_range_left - 1;
            _used_range_top = urng.Row;
            _used_range_bottom = urng.Rows.Count + _used_range_top - 1;
            _used_range_width = _used_range_right - _used_range_left + 1;
            _used_range_height = _used_range_bottom - _used_range_top + 1;

            // read formulas
            // invariant: null means not a formula
            var fd = ReadFormulas(urng, _used_range_left, _used_range_right, _used_range_top, _used_range_bottom, _used_range_width, _used_range_height);
            _formulaTable = fd.Formulas;
            _formula_box_left = fd.Left;
            _formula_box_right = fd.Right;
            _formula_box_top = fd.Top;
            _formula_box_bottom = fd.Bottom;

            // read values
            // invariant: null means empty cell
            var vd = ReadData(urng, _used_range_left, _used_range_right, _used_range_top, _used_range_bottom, _used_range_width, _used_range_height);
            _valueTable = vd.Data;
            _value_box_left = vd.Left;
            _value_box_right = vd.Right;
            _value_box_top = vd.Top;
            _value_box_bottom = vd.Bottom;
            _num_values = vd.Count;

            _time_ms_marshaling = sw.ElapsedMilliseconds;
            #endregion MARSHALING

            #region DEPENDENCE
            sw.Restart();
            // dependence table
            // invariant: table entry contains list of indices of dependency
            _referenceTable = new Dictionary<Tuple<int, int>, List<Reference>>();
            // get dependence information from formulas
            for (int row = 0; row < _formulaTable.Length; row++)
            {
                for (int col = 0; col < _formulaTable[row].Length; col++)
                {
                    // is the cell a formula?
                    if (_formulaTable[row][col] != null)
                    {
                        // parse formula
                        psw.Start();
                        ExprOpt astOpt = Parcel.parseFormula(_formulaTable[row][col], _path, _wbname, _wsname);
                        psw.Stop();
                        if (ExprOpt.get_IsSome(astOpt))
                        {
                            // it's a formula; count it
                            // we count here because we only truly know if a string is a formula
                            // when we try to parse it
                            _num_formulas++;

                            var ast = astOpt.Value;
                            // get range referencese
                            var rrefs = Parcel.rangeReferencesFromExpr(ast);
                            // get address references
                            var arefs = Parcel.addrReferencesFromExpr(ast);

                            // convert references into internal representation

                            // addresses first
                            for (int i = 0; i < arefs.Length; i++)
                            {
                                var addr = arefs[i];
                                var key = new Tuple<int, int>(row, col);
                                if (!_referenceTable.ContainsKey(key))
                                {
                                    _referenceTable.Add(key, new List<Reference>());
                                }
                                // convert to internal representation
                                _referenceTable[key].Add(ValueAddressToReference(addr));
                            }

                            // ranges next
                            for (int i = 0; i < rrefs.Length; i++)
                            {
                                var rng = rrefs[i];
                                var addrs = rng.Addresses();

                                int maxCol = Int32.MinValue;
                                int minCol = Int32.MaxValue;
                                int maxRow = Int32.MinValue;
                                int minRow = Int32.MaxValue;
                                bool onSheet = true;

                                for (int j = 0; j < addrs.Length; j++)
                                {
                                    var addr = addrs[j];

                                    var addrOffSheet = isOffSheet(addr);

                                    var key = new Tuple<int, int>(row, col);
                                    if (!_referenceTable.ContainsKey(key))
                                    {
                                        _referenceTable.Add(key, new List<Reference>());
                                    }
                                    // Excel row and column are 1-based
                                    // subtract one to make them zero-based
                                    _referenceTable[key].Add(ValueAddressToReference(addr));

                                    // find bounds
                                    if (addr.Row < minRow)
                                    {
                                        minRow = addr.Row;
                                    }

                                    if (addr.Row > maxRow)
                                    {
                                        maxRow = addr.Row;
                                    }

                                    if (addr.Col < minCol)
                                    {
                                        minCol = addr.Col;
                                    }

                                    if (addr.Col > maxCol)
                                    {
                                        maxCol = addr.Col;
                                    }

                                    // range is off-sheet if any of its addresses are off sheet
                                    if (onSheet && addrOffSheet)
                                    {
                                        onSheet = false;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // do ourselves a favor and remove entry from formula table
                            _formulaTable[row][col] = null;
                        }
                    }
                }

                // stop stopwatches and adjust
                _time_ms_dep_analysis = sw.ElapsedMilliseconds - psw.ElapsedMilliseconds;
                _time_ms_parsing = psw.ElapsedMilliseconds;
            }
            #endregion DEPENDENCE

            // release COM resources
            Marshal.ReleaseComObject(urng);
        }

        private struct FormulaData
        {
            public FormulaData(int left, int right, int top, int bottom, string[][] formulas)
            {
                Left = left;
                Right = right;
                Top = top;
                Bottom = bottom;
                Formulas = formulas;
            }

            public string[][] Formulas { get; }

            public int Left { get; }

            public int Right { get; }

            public int Top { get; }

            public int Bottom { get; }
        }

        private struct ValueData
        {
            public ValueData(int left, int right, int top, int bottom, object[][] data, int count)
            {
                Left = left;
                Right = right;
                Top = top;
                Bottom = bottom;
                Data = data;
                Count = count;
            }

            public object[][] Data { get; }

            public int Left { get; }

            public int Right { get; }

            public int Top { get; }

            public int Bottom { get; }

            public int Count { get; }
        }

        private struct Reference
        {
            public Reference(bool onSheet, int row, int col)
            {
                OnSheet = onSheet;
                Row = row;
                Col = col;
            }

            public bool OnSheet { get; }

            public int Row { get; }

            public int Col { get; }
        }
        
        private static string[][] InitStringTable(int width, int height)
        {
            var outer_y = new string[height][];
            for (int y = 0; y < height; y++)
            {
                outer_y[y] = new string[width];
            }
            return outer_y;
        }

        private static object[][] InitObjectTable(int width, int height)
        {
            var outer_y = new object[height][];
            for (int y = 0; y < height; y++)
            {
                outer_y[y] = new object[width];
            }
            return outer_y;
        }

        private AST.Address ValueReferenceToAddress(int row, int col)
        {
            var r1c1 = AST.Address.fromR1C1withMode(row + _value_box_top, col + _value_box_left, AST.AddressMode.Absolute, AST.AddressMode.Absolute, _wsname, _wbname, _path);
            return r1c1;
        }

        private AST.Address FormulaReferenceToAddress(int row, int col)
        {
            return AST.Address.fromR1C1withMode(row + _formula_box_top, col + _formula_box_left, AST.AddressMode.Absolute, AST.AddressMode.Absolute, _wsname, _wbname, _path);
        }

        private Reference FormulaAddressToReference(AST.Address addr)
        {
            Debug.Assert(InFormulaBox(addr));

            // if it's on-sheet, set flag to true
            bool onSheet = addr.Path == Path &&
                           addr.WorkbookName == Workbook &&
                           addr.WorksheetName == Worksheet;

            // x and y coordinates don't matter for off-sheet formulas
            return new Reference(
                onSheet,
                onSheet ? addr.Row - _formula_box_top : 0,
                onSheet ? addr.Col - _formula_box_left : 0);
        }

        private Reference ValueAddressToReference(AST.Address addr)
        {
            Debug.Assert(InValueBox(addr));

            // if it's on-sheet, set flag to true
            bool onSheet = addr.Path == Path &&
                           addr.WorkbookName == Workbook &&
                           addr.WorksheetName == Worksheet;

            // x and y coordinates don't matter for off-sheet formulas
            return new Reference(
                onSheet,
                onSheet ? addr.Row - _value_box_top : 0,
                onSheet ? addr.Col - _value_box_left : 0);
        }

        private static ValueData ReadData(Excel.Range urng, int left, int right, int top, int bottom, int width, int height)
        {
            // array read of data cells
            // note that this is a 1-based 2D multiarray
            // we grab this in array form so that we can avoid a COM
            // call for every blank-cell check
            object[,] data;
            // annoyingly, the return type for Value2 changes depending on the size of the range
            if (width == 1 && height == 1)
            {
                int[] lengths = new int[2] { 1, 1 };
                int[] lower_bounds = new int[2] { 1, 1 };
                data = (object[,])Array.CreateInstance(typeof(object), lengths, lower_bounds);
                data[1, 1] = urng.Value2;
            }
            else
            {   // ok, it really is an array
                data = (object[,])urng.Value2;
            }

            // if the worksheet contains nothing, data will be null
            if (data != null)
            {
                // scan data for true bounds
                int r_min = Int32.MaxValue;
                int r_max = Int32.MinValue;
                int c_min = Int32.MaxValue;
                int c_max = Int32.MinValue;

                for (int r = top; r <= bottom; r++)
                {
                    for (int c = left; c <= right; c++)
                    {
                        if (data[r - top + 1, c - left  + 1] != null)
                        {
                            if (r <= r_min)
                            {
                                r_min = r;
                            }

                            if (r >= r_max)
                            {
                                r_max = r;
                            }

                            if (c <= c_min)
                            {
                                c_min = c;
                            }

                            if (c >= c_max)
                            {
                                c_max = c;
                            }
                        }
                    }
                }

                // add one because bounds are inclusive
                var true_width = right - left + 1;
                var true_height = bottom - top + 1;

                // output
                var dataOutput = InitObjectTable(true_width, true_height);

                // for each COM object in the used range, create an address object
                // WITHOUT calling any methods on the COM object itself
                int x_old = -1;
                int x = -1;
                int y = 0;

                // count cells that contain values
                int dcount = 0;

                for (int i = 0; i < width * height; i++)
                {
                    // The basic idea here is that we know how Excel iterates over collections
                    // of cells.  The Excel.Range returned by UsedRange is always rectangular.
                    // Thus we can calculate the addresses of each COM cell reference without
                    // needing to incur the overhead of actually asking it for its address.
                    x = (x + 1) % width;
                    // increment y if x wrapped (x < x_old or x == x_old when width == 1)
                    y = x <= x_old ? y + 1 : y;

                    // don't track if the cell contains nothing
                    if (data[y + 1, x + 1] != null) // adjust indices to be one-based
                    {
                        // copy the value in the cell
                        dataOutput[y][x] = data[y + 1, x + 1];
                        dcount++;
                    }

                    x_old = x;
                }

                return new ValueData(c_min, c_max, r_min, r_max, dataOutput, dcount);
            }

            // no data
            return new ValueData(left, right, top, bottom, InitObjectTable(width, height), 0);
        }

        private static FormulaData ReadFormulas(Excel.Range urng, int left, int right, int top, int bottom, int width, int height)
        {
            // init R1C1 extractor
            var regex = new Regex("^R([0-9]+)C([0-9]+)$", RegexOptions.Compiled);

            // init formula validator
            var fn_filter = new Regex("^=", RegexOptions.Compiled);

            // if the used range is a single cell, Excel changes the type
            if (left == right && top == bottom)
            {
                var output = InitStringTable(width, height);

                var f = (string)urng.Formula;

                if (fn_filter.IsMatch(f))
                {
                    output[0][0] = f;
                }

                return new FormulaData(left, right, top, bottom, output);
            }
            else
            {
                // array read of formula cells
                // note that this is a 1-based 2D multiarray
                object[,] formulas = (object[,])urng.Formula;

                // before doing anything, ensure that the used range
                // is as tight as possible
                int c_min = Int32.MaxValue;
                int c_max = Int32.MinValue;
                int r_min = Int32.MaxValue;
                int r_max = Int32.MinValue;

                for (int c = 1; c <= width; c++)
                {
                    for (int r = 1; r <= height; r++)
                    {
                        var f = (string)formulas[r, c];
                        if (fn_filter.IsMatch(f))
                        {
                            if (c_min > c)
                            {
                                c_min = c;
                            }

                            if (c_max < c)
                            {
                                c_max = c;
                            }

                            if (r_min > r)
                            {
                                r_min = r;
                            }

                            if (r_max < r)
                            {
                                r_max = r;
                            }
                        }
                    }
                }

                // sometimes there are no formulas at all
                if (c_min == Int32.MaxValue &&
                    c_max == Int32.MinValue &&
                    r_min == Int32.MaxValue &&
                    r_max == Int32.MinValue)
                {
                    var empty = new string[1][];
                    empty[0] = new string[1];

                    return new FormulaData(1, 1, 1, 1, empty);
                }
                
                // bounds are inclusive, so add one
                var true_width = c_max - c_min + 1;
                var true_height = r_max - r_min + 1;

                // output
                var output = InitStringTable(true_width, true_height);

                // for every cell that is actually a formula, add to 
                // formula dictionary & init formula lookup dictionaries
                for (int c = c_min; c <= c_max; c++)
                {
                    for (int r = r_min; r <= r_max; r++)
                    {
                        string f = (string)formulas[r, c];
                        if (fn_filter.IsMatch(f))
                        {
                                output[r - r_min][c - c_min] = f;
                        }
                    }
                }

                return new FormulaData(c_min + left - 1, c_max + left - 1, r_min + top - 1, r_max + top - 1, output);
            }
        }

        public AST.Address[] getAllFormulaAddrs()
        {
            var addrs = new HashSet<AST.Address>();
            for (int row = 0; row < _formulaTable.Length; row++)
            {
                for (int col = 0; col < _formulaTable[row].Length; col++)
                {
                    if (_formulaTable[row][col] != null)
                    {
                        addrs.Add(FormulaReferenceToAddress(row, col));
                    }
                }
            }
            return addrs.ToArray();
        }

        private bool InFormulaBox(AST.Address addr)
        {
            return addr.Row >= _formula_box_top &&
                   addr.Row <= _formula_box_bottom &&
                   addr.Col >= _formula_box_left &&
                   addr.Col <= _formula_box_right;
        }

        private bool InValueBox(AST.Address addr)
        {
            return addr.Row >= _value_box_top &&
                   addr.Row <= _value_box_bottom &&
                   addr.Col >= _value_box_left &&
                   addr.Col <= _value_box_right;
        }

        public bool isFormula(AST.Address addr)
        {
            if (!InFormulaBox(addr))
            {
                return false;
            }

            // we arbitrarily say that off-sheet formulas are not formulas,
            // because there's no way to know otherwise
            var d = FormulaAddressToReference(addr);
            if (!d.OnSheet)
            {
                return false;
            }
            else
            {
                return _formulaTable[d.Row][d.Col] != null;
            }
        }

        public AST.Address[] allCells()
        {
            if (_cache_allCells == null)
            {
                var output = new List<AST.Address>();
                for (int row = 0; row < _valueTable.Length; row++)
                {
                    for (int col = 0; col < _valueTable[row].Length; col++)
                    {
                        if (_valueTable[row][col] != null)
                        {
                            var addr = ValueReferenceToAddress(row, col);
                            output.Add(addr);
                        }
                    }
                }

                _cache_allCells = output.ToArray();
            }

            return _cache_allCells;
        }

        // returns the union of the formula and value boxes;
        // we use this because Excel's used range is a bad
        // indicator of actual spreadsheet size
        private Tuple<int, int, int, int> UnionedBoundingBox()
        {
            int left = _formula_box_left < _value_box_left ? _formula_box_left : _value_box_left;
            int right = _formula_box_right > _value_box_right ? _formula_box_right : _value_box_right;
            int top = _formula_box_top < _value_box_top ? _formula_box_top : _value_box_top;
            int bottom = _formula_box_bottom > _value_box_bottom ? _formula_box_bottom : _value_box_bottom;
            return new Tuple<int, int, int, int>(left, right, top, bottom);
        }

        public AST.Address[] allCellsIncludingBlanks()
        {
            if (_cache_allCellsIncludingBlanks == null)
            {
                var bb = UnionedBoundingBox();
                int left = bb.Item1;
                int right = bb.Item2;
                int top = bb.Item3;
                int bottom = bb.Item4;

                int width = right - left + 1;
                int height = bottom - top + 1;

                var output = new AST.Address[width * height];
                int i = 0;
                for (int row = top; row <= bottom; row++)
                {
                    for (int col = left; col <= right; col++)
                    {
                        var addr = AST.Address.fromR1C1withMode(
                            row,
                            col,
                            AST.AddressMode.Absolute,
                            AST.AddressMode.Absolute,
                            _wsname,
                            _wbname,
                            _path
                        );
                        try
                        {
                            output[i] = addr;
                        }
                        catch (Exception e)
                        {
                            var wtf = true;
                        }
                        
                        i++;
                    }
                }

                _cache_allCellsIncludingBlanks = output;
            }

            return _cache_allCellsIncludingBlanks;
        }

        public int getPathClosureIndex(Tuple<string, string, string> path)
        {
            if (path.Item1 == _path && path.Item2 == _wbname && path.Item3 == _wsname)
            {
                return 0;
            }
            else
            {
                return 1;
            }
        }

        public string getFormulaAtAddress(AST.Address addr)
        {
            // if the formula is off-sheet return a constant function
            const string f = "=0";

            if (!InFormulaBox(addr))
            {
                return f;
            }

            var d = FormulaAddressToReference(addr);
            if (d.OnSheet)
            {
                return _formulaTable[d.Row][d.Col];
            }
            else
            {
                return f;
            }
        }

        public string readCOMValueAtAddress(AST.Address addr)
        {
            if (!InValueBox(addr))
            {
                return String.Empty;
            }

            // if the address is unresolvable or points to a
            // cell not in the dependence graph (e.g., empty cells)
            // return the empty string
            var d = ValueAddressToReference(addr);
            if (!d.OnSheet)
            {
                return String.Empty;
            }
            else
            {
                try
                {
                    var s = Convert.ToString(_valueTable[d.Row][d.Col]);
                    if (s == null)
                    {
                        return String.Empty;
                    }
                    else
                    {
                        return s;
                    }
                }
                catch (Exception)
                {
                    return String.Empty;
                }

            }
        }

        public HashSet<AST.Address> getFormulaSingleCellInputs(AST.Address addr)
        {
            if (!InFormulaBox(addr))
            {
                return new HashSet<AST.Address>();
            }

            var d = FormulaAddressToReference(addr);
            var output = new HashSet<AST.Address>();
            if (d.OnSheet)
            {
                var key = new Tuple<int,int>(d.Row, d.Col);

                // formulas of the form =1 + 1 are indeed formulas,
                // but they have no references, so don't bother looking
                // in this case.
                if (_referenceTable.ContainsKey(key))
                {
                    foreach (Reference d2 in _referenceTable[key])
                    {
                        var addr2 = ValueReferenceToAddress(d2.Row, d2.Col);
                        output.Add(addr2);
                    }
                }
            }

            return output;
        }

        public void Dispose()
        {
            Marshal.ReleaseComObject(_valueTable);
        }
    }
}
