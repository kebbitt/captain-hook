using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace CaptainHook.Common.Rules
{
    /// <summary>
    /// Represents a JSONPath filter expression to create rule based routing rules.
    /// </summary>
    public class JsonPathFilter
    {
        /// <summary>
        /// The JSONPath expression.
        ///     See https://goessner.net/articles/JsonPath/ for more details on JSONPath expressions.
        /// </summary>
        [Required]
        public string Expression { get; set; }

        /// <summary>
        /// The <see cref="JsonPathFilterOperator"/> to apply to the return of the expression.
        /// </summary>
        public JsonPathFilterOperator Operator { get; set; }

        /// <summary>
        /// The target for some of the operators usable on the JSONPath expression.
        /// </summary>
        public string OperatorTarget { get; set; }
    }

    /// <summary>
    /// A list of supported operators to use on <see cref="JsonPathFilter"/>.
    /// </summary>
    [DefaultValue(Equal)]
    public enum JsonPathFilterOperator
    {
        Equal              =  1,
        NotEqual           =  2,
        Empty              = 11,
        NotEmpty           = 12,
        GreaterThan        = 21,
        GreaterOrEqualThan = 22,
        LesserThan         = 31,
        LesserOrEqualThan  = 32
    }
}
