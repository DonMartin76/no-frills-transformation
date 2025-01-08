using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using NoFrillsTransformation.Interfaces;

namespace NoFrillsTransformation.Operators
{
    [Export(typeof(NoFrillsTransformation.Interfaces.IOperator))]
    public class EqualsOperator : AbstractOperator, IOperator
    {
        public EqualsOperator()
        {
            Type = ExpressionType.Equals;
            Name = "equals";
            ParamCount = 2;
            ParamTypes = new ParamType[] { ParamType.Any, ParamType.Any };
            ReturnType = ParamType.Bool;
        }

        private bool _ignoreCase = false;
        private bool _ignoreCrLf = false;

        public string Evaluate(IEvaluator eval, IExpression expression, IContext context)
        {
            var compType = _ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture;
            var a = eval.Evaluate(eval, expression.Arguments[0], context);
            var b = eval.Evaluate(eval, expression.Arguments[1], context);
            if (_ignoreCrLf)
            {
                a = ReplaceCrLf(a);
                b = ReplaceCrLf(b);
            }
            return BoolToString(a.Equals(b, compType));
        }

        private static string ReplaceCrLf(string s)
        {
            return s.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        public override void Configure(string? config)
        {
            if (config == null)
                return;
            var options = config.Split(',').Select(x => x.ToLowerInvariant().Trim());
            _ignoreCase = options.Contains("ignorecase");
            _ignoreCrLf = options.Contains("ignorecrlf");
        }
    }
}
