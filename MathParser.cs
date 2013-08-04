using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MathParser
{
    internal delegate double OpMethod(params double[] obj);

    internal struct Op
    {
        public Op(string n, string s, OpMethod m) { Name = n; Sign = s; Method = m; }
        public Op(string n, OpMethod m) :this(n,null,m) {}

        internal string Name; // Operatorname
        internal string Sign; // Infix-Operator-Zeichen
        internal OpMethod Method; // Methoden-Delegat
    }

    public abstract class Term
    {
        // Liste der bekannten Operationen (Infix-Operatoren nach dem Rang geordnet)
        static Op[] ops = new Op[]
        {
            new Op("pow", "^", (xs) => Math.Pow(xs[0],xs[1]) ),
            new Op("mul", "*", (xs) => xs[0] * xs[1] ),
            new Op("div", "/", (xs) => xs[0] / xs[1] ),
            new Op("add", "+", (xs) => xs[0] + xs[1] ),
            new Op("sub", "-", (xs) => xs[0] - xs[1] ),

            new Op("sin", (xs) => Math.Sin(xs[0]) ),
            new Op("cos", (xs) => Math.Cos(xs[0])),
            new Op("tan", (xs) => Math.Tan(xs[0])),
            new Op("sqrt", (xs) => Math.Sqrt(xs[0])),
            new Op("sign", (xs) => Math.Sign(xs[0])),
            new Op("ln", (xs) => Math.Log(xs[0])),
            new Op("exp", (xs) => Math.Exp(xs[0])),
            new Op("max", (xs) => Math.Max(xs[0], xs[1])),
            new Op("min", (xs) => Math.Min(xs[0], xs[1])),
            new Op("", (xs) => xs[0]),
        };

        static string infixPattern =    @"^((?:(?<O>\()|(?<-O>\))|[^\(\)])*)\s*(?(O)(?!)|{0})\s*(.*)$";
        static string praefixPattern =  @"^(?<name>[a-z]*)\((?<tail>.*)\)$";
        static string commaPattern =    @"(?<=(?:(?<O>\()|(?<-O>\))|(?(O)[^\(\)]|[^\(\),]))*)(?(O)(?!)),";
        static string variablePattern = @"^[a-z]+(?![a-z\(])$";

        public static Term Parse(string s)
        {
            // 0.   Vorverarbeitung
            //      Infix Operatoren der höchsten Ebene in Präfix umwandeln
            //      (Infix Ops müssen zwangsläufig zwei-parametrig sein)
            foreach (Op o in ops.Reverse().Where((x) => x.Sign != null))
            {
                string opPattern = String.Format(infixPattern, Regex.Escape(o.Sign));
                s = Regex.Replace(s, opPattern, String.Format("{0}($1,$2)", o.Name));
            }

            // Ausgabe hier, zeigt den ParseBaum
             Console.WriteLine(s);

            // 1.   Eigentlichtes Parsen
            // 1.a) check ob Operation
            Match m = Regex.Match(s, praefixPattern);
            if (m.Success)
            {
                // neuen OperatorTerm erzeugen
                Operation result = new Operation();
                // konkreten Operationstyp zuweisen (anhand des Namens finden)
                result.O = ops.First((x) => (x.Name == m.Groups["name"].Value));
                // Parameter parsen (an Kommata splitten und einzeln parsen)
                result.SubTerms = Regex.Split(m.Groups["tail"].Value, commaPattern)
                                       .Select((x) => Parse(x))
                                       .ToArray();

                return result;
            }

            // 1.b) check ob Zahl
            double value;
            if (Double.TryParse(s, out value))
                return new Constant(value);

            // 1.c) check ob Variable
            if (Regex.IsMatch(s, variablePattern))
                return new Variable(s);

            // 1.d) sonst 0
            return new Constant(0);
        }

        public double Eval(params double[] xs)
        {
            // internes Eval aufrufen mit ausschließlich ungebundenen Variablen
            return Eval(new Dictionary<string, double>(), new Queue<double>(xs));
        }

        internal abstract double Eval(Dictionary<string, double> boundXs, Queue<double> unboundXs);
    }

    internal class Operation : Term
    {
        internal Op O;
        internal Term[] SubTerms;
        public override string ToString()
        {
            if (O.Sign != null) // falls Infix-Notation möglich
                return String.Join(O.Sign, SubTerms.Select((x) => x.ToString()).ToArray());
            else // sonst Prefix-Notation
                return String.Format("{0}({1})",
                    O.Name,
                    String.Join(",", SubTerms.Select((x) => x.ToString()).ToArray()));

        }
        internal override double Eval(Dictionary<string, double> boundXs, Queue<double> unboundXs)
        {
            return O.Method(SubTerms.Select((x) => x.Eval(boundXs, unboundXs)).ToArray());
        }
    }

    internal class Constant : Term
    {
        internal double Value;
        internal Constant(double v) { Value = v; }
        public override string ToString() { return Value.ToString(); }
        internal override double Eval(Dictionary<string, double> boundXs, Queue<double> unboundXs)
        { return Value; }
    }

    internal class Variable : Term
    {
        internal string Name;
        internal Variable(string n) { Name = n; }
        public override string ToString() { return Name; }
        internal override double Eval(Dictionary<string, double> boundXs, Queue<double> unboundXs)
        {
            // prüfen ob Variable bereits vergeben, sonst nächste schnappen
            if (!boundXs.ContainsKey(Name))
                boundXs.Add(Name, unboundXs.Dequeue());

            return boundXs[Name];
        }
    }
}
