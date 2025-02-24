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
    public class AcumaticaLocalizationMd5HashOperator : IOperator
    {
        public string Name => "aculocalizationmd5hash";

        public string Description => "Creates an MD5 hash string from the input string, according to Localization.";

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
            return CalculateMD5LocalizationString(parameter);
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

        internal static Byte[] CalculateMD5(String str)
        {
            if (str == null) return new Byte[0];

            using var prov = System.Security.Cryptography.MD5.Create();
            byte[] bs = prov.ComputeHash(System.Text.Encoding.Unicode.GetBytes(str));

            return bs;
        }
        internal static String CalculateMD5String(String str)
        {
            return ConvertBytes(CalculateMD5(str));
        }
        internal static String CalculateMD5LocalizationString(String str)
        {
            return CalculateMD5String(str.ToLower());
        }

        internal static String ConvertBytes(Byte[] bytes)
        {
            return ConvertBytes(bytes, 0);
        }
        /// <remarks>Will insert a separator (<c>-</c>) every <paramref name="chars"/> characters</remarks>
        internal static String ConvertBytes(Byte[] bytes, Int32 chars)
        {
            if (bytes == null) return "";

            StringBuilder s = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0 && chars > 0 && i % chars == 0) s.Append("-");

                s.Append(bytes[i].ToString("X2"));
            }
            return s.ToString();
        }
    }
}