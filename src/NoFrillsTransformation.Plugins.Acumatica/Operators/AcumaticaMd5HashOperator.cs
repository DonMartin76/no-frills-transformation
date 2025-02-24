// Implements an IOperator which takes one single string,
// supposedly a date string such as "2022-06-02T09:25:36.0830000", and strips
// off the zeroes at then end of the string. It also replaces the "T" with a space.
// If there are more than three decimals which are non zero, truncate to three.

using System;
using System.Composition;
using System.Text;
using System.Text.RegularExpressions;
using NoFrillsTransformation.Interfaces;

namespace NoFrillsTransformation.Plugins.Acumatica.Operators
{
    [Export(typeof(IOperator))]
    public class AcumaticaMd5HashOperator : IOperator
    {
        public string Name => "acumd5hash";

        public string Description => "Creates an MD5 hash string from the input string.";

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
            return GetMD5Hash(parameter);
        }

        private static string GetMD5Hash(string input)
        {
            // step 1, calculate MD5 hash from input
            var md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hash = md5.ComputeHash(inputBytes);

            // step 2, convert byte array to hex string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }

            return sb.ToString();
        }
   }
}