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
    public class AcumaticaDateTimeOperator : IOperator
    {
        public string Name => "acudatetime";

        public string Description => "Converts a date string to an Acumatica compatible format.";

        public ExpressionType Type => ExpressionType.Custom;

        public int ParamCount => 1;

        public ParamType[]? ParamTypes => new ParamType[] { ParamType.String };

        public ParamType ReturnType => ParamType.String;

        public void Configure(string? config)
        {
        }

        public string Evaluate(IEvaluator eval, IExpression expression, IContext context)
        {
            string parameter = eval.Evaluate(eval, expression.Arguments[0], context);
            return Execute(parameter);
        }

        private string Execute(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            string pattern = @"(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})\.(\d+)";
            var match = Regex.Match(input, pattern);
            if (match.Success)
            {
                string date = match.Groups[1].Value;
                string decimals = match.Groups[2].Value.TrimEnd('0');
                if (string.IsNullOrEmpty(decimals))
                {
                    return date.Replace('T', ' ');
                }
                return $"{date.Replace('T', ' ')}.{decimals}";
            }
            return input.Replace('T', ' ');
        }
    }
}