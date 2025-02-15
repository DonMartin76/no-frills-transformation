// Implements an IOperator which takes one single string,
// supposedly a date string such as "2022-06-02T09:25:36.0830000", and strips
// off the zeroes at then end of the string. It also replaces the "T" with a space.
// If there are more than three decimals which are non zero, truncate to three.

using System;
using System.Composition;
using System.Text.RegularExpressions;
using NoFrillsTransformation.Interfaces;

namespace NoFrillsTransformation.Plugins.Acumatica.Operators
{
    [Export(typeof(IOperator))]
    public class AcumaticaBoolOperator : IOperator
    {
        public string Name => "acubool";

        public string Description => "Maps values of true/True/1 to 1 and false/False/0 to 0.";

        public ExpressionType Type => ExpressionType.Custom;

        public int ParamCount => 1;

        public ParamType[]? ParamTypes => new ParamType[] { ParamType.String };

        public ParamType ReturnType => ParamType.Int;

        public void Configure(string? config)
        {
        }

        public string Evaluate(IEvaluator eval, IExpression expression, IContext context)
        {
            string parameter = eval.Evaluate(eval, expression.Arguments[0], context).ToLowerInvariant();
            switch (parameter)
            {
                case "true":
                case "1":
                    return "1";
                case "false":
                case "0":
                    return "0";
            }
            return "0";
        }
    }
}