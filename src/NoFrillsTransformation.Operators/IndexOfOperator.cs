using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using NoFrillsTransformation.Interfaces;

namespace NoFrillsTransformation.Operators
{
    [Export(typeof(NoFrillsTransformation.Interfaces.IOperator))]
    public class IndexOfOperator : AbstractOperator, IOperator
    {
        public IndexOfOperator()
        {
            Type = ExpressionType.Contains;
            Name = "indexof";
            ParamCount = 2;
            ParamTypes = new ParamType[] { ParamType.Any, ParamType.Any };
            ReturnType = ParamType.Int;
        }

        private bool _ignoreCase = false;
        public string Evaluate(IEvaluator eval, IExpression expression, IContext context)
        {
            string a = eval.Evaluate(eval, expression.Arguments[0], context);
            string b = eval.Evaluate(eval, expression.Arguments[1], context);
            if (_ignoreCase)
            {
                a = a.ToUpperInvariant();
                b = b.ToUpperInvariant();
            }
            return IntToString(a.IndexOf(b));
        }

        public override void Configure(string? config)
        {
            _ignoreCase = config != null ? config.Equals("ignorecase", StringComparison.InvariantCultureIgnoreCase) : false;
        }
    }
}
