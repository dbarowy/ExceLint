﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using COMWrapper;
using System.Linq;
using ExceLint;
using FastDependenceAnalysis;

namespace ExceLintTests
{
    [TestClass]
    public class BasicTests
    {
        private Graphs _addressModeDAG;

        public BasicTests()
        {
            _addressModeDAG = AddressModeDAG();
        }

        // gets the set of shallow intransitive mixed input vectors pointed to by the formula
        private static Tuple<int,int,int>[] getSIMIVs(AST.Address formula, Graph dag)
        {
            var vs = ExceLint.Vector.getRebasedVectors(formula, dag, isMixed: true, isTransitive: false, isFormula: true, isOffSheetInsensitive: true, isRelative: true, includeConstant: false);
            return vs.Select(v =>
            {
                if (v.IsConstant)
                {
                    var c = (ExceLint.Vector.RelativeVector.Constant)v;
                    return new Tuple<int, int, int>(c.Item1, c.Item2, c.Item3);
                }
                else
                {
                    var c = (ExceLint.Vector.RelativeVector.NoConstant)v;
                    return new Tuple<int, int, int>(c.Item1, c.Item2, c.Item3);
                }
            }).ToArray();
        }

        private Graphs AddressModeDAG()
        {
            var app = new Application();
            var wb = app.OpenWorkbook(@"..\..\TestData\AddressModes.xlsx");
            var graph = wb.buildDependenceGraph();
            return graph;
        }

        [TestMethod]
        public void absoluteSingleVectorSIMIV()
        {
            // tests that $A$2 in cell B2 on the same sheet returns the vector (1,2,0)
            var graph = _addressModeDAG.Worksheets[0];
            var wbname = graph.Workbook;
            var wsname = graph.Worksheet;
            var path = graph.Path;
            var formula = AST.Address.fromA1withMode(2, "B", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);
            var vectors = getSIMIVs(formula, graph);
            Assert.IsTrue(vectors.Length == 1);
            var foo = vectors[0];
            var bar = new Tuple<int, int, int>(1, 2, 0);
            Assert.IsTrue(foo.Equals(bar));
        }

        [TestMethod]
        public void relativeRowAbsoluteColumnSingleVectorSIMIV()
        {
            // tests that A$3 in cell B3 on the same sheet returns the vector (-1,3,0)
            var graph = _addressModeDAG.Worksheets[0];
            var wbname = graph.Workbook;
            var wsname = graph.Worksheet;
            var path = graph.Path;
            var formula = AST.Address.fromA1withMode(3, "B", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);
            var vectors = getSIMIVs(formula, graph);
            Assert.IsTrue(vectors.Length == 1);
            var foo = vectors[0];
            var bar = new Tuple<int, int, int>(-1, 3, 0);
            Assert.IsTrue(foo.Equals(bar));
        }

        [TestMethod]
        public void absoluteRowRelativeColumnSingleVectorSIMIV()
        {
            // tests that $A4 in cell B4 on the same sheet returns the vector (1,0,0)
            var graph = _addressModeDAG.Worksheets[0];
            var wbname = graph.Workbook;
            var wsname = graph.Worksheet;
            var path = graph.Path;
            var formula = AST.Address.fromA1withMode(4, "B", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);
            var vectors = getSIMIVs(formula, graph);
            Assert.IsTrue(vectors.Length == 1);
            var foo = vectors[0];
            var bar = new Tuple<int, int, int>(1, 0, 0);
            Assert.IsTrue(foo.Equals(bar));
        }

        [TestMethod]
        public void relativeSingleVectorSIMIV()
        {
            // tests that A5 in cell B5 on the same sheet returns the vector (-1,0,0)
            var graph = _addressModeDAG.Worksheets[0];
            var wbname = graph.Workbook;
            var wsname = graph.Worksheet;
            var path = graph.Path;
            var formula = AST.Address.fromA1withMode(5, "B", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);
            var vectors = getSIMIVs(formula, graph);
            Assert.IsTrue(vectors.Length == 1);
            var foo = vectors[0];
            var bar = new Tuple<int, int, int>(-1, 0, 0);
            Assert.IsTrue(foo.Equals(bar));
        }

        [TestMethod]
        public void AbsAbsRangeVectorsSIMIV()
        {
            // tests that $A$2:$A$5 in cell C2 on the same sheet returns a set of absolute vectors
            var graph = _addressModeDAG.Worksheets[0];
            var wbname = graph.Workbook;
            var wsname = graph.Worksheet;
            var path = graph.Path;
            var formula = AST.Address.fromA1withMode(2, "C", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);
            var vectors = getSIMIVs(formula, graph);
            Assert.IsTrue(vectors.Length == 4);
            Assert.IsTrue(Array.Exists(vectors, e => e.Equals(new Tuple<int,int,int>(1, 2, 0))));
            Assert.IsTrue(Array.Exists(vectors, e => e.Equals(new Tuple<int, int, int>(1, 3, 0))));
            Assert.IsTrue(Array.Exists(vectors, e => e.Equals(new Tuple<int, int, int>(1, 4, 0))));
            Assert.IsTrue(Array.Exists(vectors, e => e.Equals(new Tuple<int, int, int>(1, 5, 0))));
        }

        [TestMethod]
        public void RelAbsRangeVectorsSIMIV()
        {
            // tests that A$2:A$5 in cell C3 on the same sheet returns a set of mixed vectors
            var graph = _addressModeDAG.Worksheets[0];
            var wbname = graph.Workbook;
            var wsname = graph.Worksheet;
            var path = graph.Path;
            var formula = AST.Address.fromA1withMode(3, "C", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);
            var vectors = getSIMIVs(formula, graph);
            Assert.IsTrue(vectors.Length == 4);
            Assert.IsTrue(Array.Exists(vectors, e => e.Equals(new Tuple<int, int, int>(-2, 2, 0))));
            Assert.IsTrue(Array.Exists(vectors, e => e.Equals(new Tuple<int, int, int>(-2, 3, 0))));
            Assert.IsTrue(Array.Exists(vectors, e => e.Equals(new Tuple<int, int, int>(-2, 4, 0))));
            Assert.IsTrue(Array.Exists(vectors, e => e.Equals(new Tuple<int, int, int>(-2, 5, 0))));
        }

        [TestMethod]
        public void AbsRelRangeVectorsSIMIV()
        {
            // tests that $A2:$A5 in cell C4 on the same sheet returns a set of mixed vectors
            var graph = _addressModeDAG.Worksheets[0];
            var wbname = graph.Workbook;
            var wsname = graph.Worksheet;
            var path = graph.Path;
            var formula = AST.Address.fromA1withMode(4, "C", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);
            var vectors = getSIMIVs(formula, graph);
            Assert.IsTrue(vectors.Length == 4);
            Assert.IsTrue(Array.Exists(vectors, e => e.Equals(new Tuple<int, int, int>(1, -2, 0))));
            Assert.IsTrue(Array.Exists(vectors, e => e.Equals(new Tuple<int, int, int>(1, -1, 0))));
            Assert.IsTrue(Array.Exists(vectors, e => e.Equals(new Tuple<int, int, int>(1,  0, 0))));
            Assert.IsTrue(Array.Exists(vectors, e => e.Equals(new Tuple<int, int, int>(1,  1, 0))));
        }

        [TestMethod]
        public void RelRelRangeVectorsSIMIV()
        {
            // tests that A2:A5 in cell C5 on the same sheet returns a set of mixed vectors
            var graph = _addressModeDAG.Worksheets[0];
            var wbname = graph.Workbook;
            var wsname = graph.Worksheet;
            var path = graph.Path;
            var formula = AST.Address.fromA1withMode(5, "C", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);
            var vectors = getSIMIVs(formula, graph);
            Assert.IsTrue(vectors.Length == 4);
            Assert.IsTrue(Array.Exists(vectors, e => e.Equals(new Tuple<int, int, int>(-2, -3, 0))));
            Assert.IsTrue(Array.Exists(vectors, e => e.Equals(new Tuple<int, int, int>(-2, -2, 0))));
            Assert.IsTrue(Array.Exists(vectors, e => e.Equals(new Tuple<int, int, int>(-2, -1, 0))));
            Assert.IsTrue(Array.Exists(vectors, e => e.Equals(new Tuple<int, int, int>(-2,  0, 0))));
        }

        [TestMethod]
        public void IsFormulaTest()
        {
            using (var app = new Application())
            {
                using (var wb = app.OpenWorkbook(@"..\..\TestData\DataTest.xlsx"))
                {
                    var p = Progress.NOPProgress();
                    var graph = wb.buildDependenceGraph().Worksheets[0];
                    var conf = (new FeatureConf()).enableShallowInputVectorMixedFullCVectorResultantOSI(true);
                    var m = ModelBuilder.initEntropyModel(app.XLApplication(), conf, graph, p);
                    var ih = m.InvertedHistogram;

                    var wbname = graph.Workbook;
                    var wsname = graph.Worksheet;
                    var path = graph.Path;

                    // A1
                    var a1_addr = AST.Address.fromA1withMode(1, "A", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // B1
                    var b1_addr = AST.Address.fromA1withMode(1, "B", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // C1
                    var c1_addr = AST.Address.fromA1withMode(1, "C", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // A2
                    var a2_addr = AST.Address.fromA1withMode(2, "A", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // B2
                    var b2_addr = AST.Address.fromA1withMode(2, "B", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // C2
                    var c2_addr = AST.Address.fromA1withMode(2, "C", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // A3
                    var a3_addr = AST.Address.fromA1withMode(3, "A", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    Assert.IsFalse(EntropyModelBuilder2.AddressIsFormulaValued(a1_addr, ih, graph));
                    Assert.IsFalse(EntropyModelBuilder2.AddressIsFormulaValued(b1_addr, ih, graph));
                    Assert.IsFalse(EntropyModelBuilder2.AddressIsFormulaValued(c1_addr, ih, graph));
                    Assert.IsFalse(EntropyModelBuilder2.AddressIsFormulaValued(a2_addr, ih, graph));
                    Assert.IsFalse(EntropyModelBuilder2.AddressIsFormulaValued(b2_addr, ih, graph));
                    Assert.IsTrue(EntropyModelBuilder2.AddressIsFormulaValued(c2_addr, ih, graph));
                    Assert.IsFalse(EntropyModelBuilder2.AddressIsFormulaValued(a3_addr, ih, graph));
                }
            }
        }

        [TestMethod]
        public void IsNumericTest()
        {
            using (var app = new Application())
            {
                using (var wb = app.OpenWorkbook(@"..\..\TestData\DataTest.xlsx"))
                {
                    var p = Progress.NOPProgress();
                    var graph = wb.buildDependenceGraph().Worksheets[0];
                    var conf = (new FeatureConf()).enableShallowInputVectorMixedFullCVectorResultantOSI(true);
                    var m = ModelBuilder.initEntropyModel(app.XLApplication(), conf, graph, p);
                    var ih = m.InvertedHistogram;

                    var wbname = graph.Workbook;
                    var wsname = graph.Worksheet;
                    var path = graph.Path;

                    // A1
                    var a1_addr = AST.Address.fromA1withMode(1, "A", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // B1
                    var b1_addr = AST.Address.fromA1withMode(1, "B", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // C1
                    var c1_addr = AST.Address.fromA1withMode(1, "C", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // A2
                    var a2_addr = AST.Address.fromA1withMode(2, "A", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // B2
                    var b2_addr = AST.Address.fromA1withMode(2, "B", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // C2
                    var c2_addr = AST.Address.fromA1withMode(2, "C", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // A3
                    var a3_addr = AST.Address.fromA1withMode(3, "A", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    Assert.IsFalse(EntropyModelBuilder2.AddressIsNumericValued(a1_addr, ih, graph));
                    Assert.IsFalse(EntropyModelBuilder2.AddressIsNumericValued(b1_addr, ih, graph));
                    Assert.IsFalse(EntropyModelBuilder2.AddressIsNumericValued(c1_addr, ih, graph));
                    Assert.IsTrue(EntropyModelBuilder2.AddressIsNumericValued(a2_addr, ih, graph));
                    Assert.IsTrue(EntropyModelBuilder2.AddressIsNumericValued(b2_addr, ih, graph));
                    Assert.IsFalse(EntropyModelBuilder2.AddressIsNumericValued(c2_addr, ih, graph));
                    Assert.IsFalse(EntropyModelBuilder2.AddressIsNumericValued(a3_addr, ih, graph));
                }
            }
        }

        [TestMethod]
        public void IsStringTest()
        {
            using (var app = new Application())
            {
                using (var wb = app.OpenWorkbook(@"..\..\TestData\DataTest.xlsx"))
                {
                    var p = Progress.NOPProgress();
                    var graph = wb.buildDependenceGraph().Worksheets[0];
                    var conf = (new FeatureConf()).enableShallowInputVectorMixedFullCVectorResultantOSI(true);
                    var m = ModelBuilder.initEntropyModel(app.XLApplication(), conf, graph, p);
                    var ih = m.InvertedHistogram;

                    var wbname = graph.Workbook;
                    var wsname = graph.Worksheet;
                    var path = graph.Path;

                    // A1
                    var a1_addr = AST.Address.fromA1withMode(1, "A", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // B1
                    var b1_addr = AST.Address.fromA1withMode(1, "B", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // C1
                    var c1_addr = AST.Address.fromA1withMode(1, "C", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // A2
                    var a2_addr = AST.Address.fromA1withMode(2, "A", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // B2
                    var b2_addr = AST.Address.fromA1withMode(2, "B", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // C2
                    var c2_addr = AST.Address.fromA1withMode(2, "C", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // A3
                    var a3_addr = AST.Address.fromA1withMode(3, "A", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    Assert.IsTrue(EntropyModelBuilder2.AddressIsStringValued(a1_addr, ih, graph));
                    Assert.IsTrue(EntropyModelBuilder2.AddressIsStringValued(b1_addr, ih, graph));
                    Assert.IsTrue(EntropyModelBuilder2.AddressIsStringValued(c1_addr, ih, graph));
                    Assert.IsFalse(EntropyModelBuilder2.AddressIsStringValued(a2_addr, ih, graph));
                    Assert.IsFalse(EntropyModelBuilder2.AddressIsStringValued(b2_addr, ih, graph));
                    Assert.IsFalse(EntropyModelBuilder2.AddressIsStringValued(c2_addr, ih, graph));
                    Assert.IsFalse(EntropyModelBuilder2.AddressIsStringValued(a3_addr, ih, graph));
                }
            }
        }

        [TestMethod]
        public void IsWhitespaceTest()
        {
            using (var app = new Application())
            {
                using (var wb = app.OpenWorkbook(@"..\..\TestData\DataTest.xlsx"))
                {
                    var p = Progress.NOPProgress();
                    var graph = wb.buildDependenceGraph().Worksheets[0];
                    var conf = (new FeatureConf()).enableShallowInputVectorMixedFullCVectorResultantOSI(true);
                    var m = ModelBuilder.initEntropyModel(app.XLApplication(), conf, graph, p);
                    var ih = m.InvertedHistogram;

                    var wbname = graph.Workbook;
                    var wsname = graph.Worksheet;
                    var path = graph.Path;

                    // A1
                    var a1_addr = AST.Address.fromA1withMode(1, "A", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // B1
                    var b1_addr = AST.Address.fromA1withMode(1, "B", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // C1
                    var c1_addr = AST.Address.fromA1withMode(1, "C", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // A2
                    var a2_addr = AST.Address.fromA1withMode(2, "A", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // B2
                    var b2_addr = AST.Address.fromA1withMode(2, "B", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // C2
                    var c2_addr = AST.Address.fromA1withMode(2, "C", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    // A3
                    var a3_addr = AST.Address.fromA1withMode(3, "A", AST.AddressMode.Absolute, AST.AddressMode.Absolute, wsname, wbname, path);

                    Assert.IsFalse(EntropyModelBuilder2.AddressIsWhitespaceValued(a1_addr, ih, graph));
                    Assert.IsFalse(EntropyModelBuilder2.AddressIsWhitespaceValued(b1_addr, ih, graph));
                    Assert.IsFalse(EntropyModelBuilder2.AddressIsWhitespaceValued(c1_addr, ih, graph));
                    Assert.IsFalse(EntropyModelBuilder2.AddressIsWhitespaceValued(a2_addr, ih, graph));
                    Assert.IsFalse(EntropyModelBuilder2.AddressIsWhitespaceValued(b2_addr, ih, graph));
                    Assert.IsFalse(EntropyModelBuilder2.AddressIsWhitespaceValued(c2_addr, ih, graph));
                    Assert.IsTrue(EntropyModelBuilder2.AddressIsWhitespaceValued(a3_addr, ih, graph));
                }
            }
        }
    }
}
