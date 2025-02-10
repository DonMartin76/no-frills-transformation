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
    public class AcumaticaDateTimeNowOperator : IOperator
    {
        public string Name => "acudatetimenow";

        public string Description => "Outputs the current date and time in an Acumatica compatible format.";

        public ExpressionType Type => ExpressionType.Custom;

        public int ParamCount => 0;

        public ParamType[]? ParamTypes => [];

        public ParamType ReturnType => ParamType.String;

        public void Configure(string? config)
        {
        }

        public string Evaluate(IEvaluator eval, IExpression expression, IContext context)
        {
            // Return a date time for "now" in this form: "2022-06-02 09:25:36.083"
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }
   }
}