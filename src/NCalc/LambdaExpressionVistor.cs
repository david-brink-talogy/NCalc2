using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NCalc.Domain;
using L = System.Linq.Expressions;

namespace NCalc
{
    internal class LambdaExpressionVistor : LogicalExpressionVisitor
    {
        private readonly IDictionary<string, object> _parameters;
        private readonly L.Expression _context;
        private readonly EvaluateOptions _options = EvaluateOptions.None;
        private readonly Dictionary<Type, HashSet<Type>> _implicitPrimitiveConversionTable = new() {
            { typeof(sbyte), new HashSet<Type> { typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) }},
            { typeof(byte), new HashSet<Type> { typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) }},
            { typeof(short), new HashSet<Type> { typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) }},
            { typeof(ushort), new HashSet<Type> { typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) }},
            { typeof(int), new HashSet<Type> { typeof(long), typeof(float), typeof(double), typeof(decimal) }},
            { typeof(uint), new HashSet<Type> { typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) }},
            { typeof(long), new HashSet<Type> { typeof(float), typeof(double), typeof(decimal) }},
            { typeof(char), new HashSet<Type> { typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) }},
            { typeof(float), new HashSet<Type> { typeof(double) }},
            { typeof(ulong), new HashSet<Type> { typeof(float), typeof(double), typeof(decimal) }},
        };

        private bool Ordinal { get { return (_options & EvaluateOptions.MatchStringsOrdinal) == EvaluateOptions.MatchStringsOrdinal; } }
        private bool IgnoreCaseString { get { return (_options & EvaluateOptions.MatchStringsWithIgnoreCase) == EvaluateOptions.MatchStringsWithIgnoreCase; } }
        private bool Checked { get { return (_options & EvaluateOptions.OverflowProtection) == EvaluateOptions.OverflowProtection; } }

        public LambdaExpressionVistor(IDictionary<string, object> parameters, EvaluateOptions options)
        {
            _parameters = parameters;
            _options = options;
        }

        public LambdaExpressionVistor(L.ParameterExpression context, EvaluateOptions options)
        {
            _context = context;
            _options = options;
        }

        public L.Expression Result { get; private set; }

        public override void Visit(LogicalExpression expression)
        {
            throw new NotImplementedException();
        }

        public override void Visit(TernaryExpression expression)
        {
            expression.LeftExpression.Accept(this);
            var test = Result;

            expression.MiddleExpression.Accept(this);
            var ifTrue = Result;

            expression.RightExpression.Accept(this);
            var ifFalse = Result;

            Result = L.Expression.Condition(test, ifTrue, ifFalse);
        }

        public override void Visit(BinaryExpression expression)
        {
            expression.LeftExpression.Accept(this);
            var left = Result;

            expression.RightExpression.Accept(this);
            var right = Result;

            switch (expression.Type)
            {
                case BinaryExpressionType.And:
                    Result = L.Expression.AndAlso(left, right);
                    break;
                case BinaryExpressionType.Or:
                    Result = L.Expression.OrElse(left, right);
                    break;
                case BinaryExpressionType.NotEqual:
                    Result = WithCommonNumericType(left, right, L.Expression.NotEqual, expression.Type);
                    break;
                case BinaryExpressionType.LesserOrEqual:
                    Result = WithCommonNumericType(left, right, L.Expression.LessThanOrEqual, expression.Type);
                    break;
                case BinaryExpressionType.GreaterOrEqual:
                    Result = WithCommonNumericType(left, right, L.Expression.GreaterThanOrEqual, expression.Type);
                    break;
                case BinaryExpressionType.Lesser:
                    Result = WithCommonNumericType(left, right, L.Expression.LessThan, expression.Type);
                    break;
                case BinaryExpressionType.Greater:
                    Result = WithCommonNumericType(left, right, L.Expression.GreaterThan, expression.Type);
                    break;
                case BinaryExpressionType.Equal:
                    Result = WithCommonNumericType(left, right, L.Expression.Equal, expression.Type);
                    break;
                case BinaryExpressionType.Minus:
                    if (Checked) Result = WithCommonNumericType(left, right, L.Expression.SubtractChecked);
                    else Result = WithCommonNumericType(left, right, L.Expression.Subtract);
                    break;
                case BinaryExpressionType.Plus:
                    if (Checked) Result = WithCommonNumericType(left, right, L.Expression.AddChecked);
                    else Result = WithCommonNumericType(left, right, L.Expression.Add);
                    break;
                case BinaryExpressionType.Modulo:
                    Result = WithCommonNumericType(left, right, L.Expression.Modulo);
                    break;
                case BinaryExpressionType.Div:
                    Result = WithCommonNumericType(left, right, L.Expression.Divide);
                    break;
                case BinaryExpressionType.Times:
                    if (Checked) Result = WithCommonNumericType(left, right, L.Expression.MultiplyChecked);
                    else Result = WithCommonNumericType(left, right, L.Expression.Multiply);
                    break;
                case BinaryExpressionType.BitwiseOr:
                    Result = L.Expression.Or(left, right);
                    break;
                case BinaryExpressionType.BitwiseAnd:
                    Result = L.Expression.And(left, right);
                    break;
                case BinaryExpressionType.BitwiseXOr:
                    Result = L.Expression.ExclusiveOr(left, right);
                    break;
                case BinaryExpressionType.LeftShift:
                    Result = L.Expression.LeftShift(left, right);
                    break;
                case BinaryExpressionType.RightShift:
                    Result = L.Expression.RightShift(left, right);
                    break;
                default:
                    throw new EvaluationException($"Expression of type {expression.Type} is not supported");
            }
        }

        public override void Visit(UnaryExpression expression)
        {
            expression.Expression.Accept(this);
            Result = expression.Type switch
            {
                UnaryExpressionType.Not => L.Expression.Not(Result),
                UnaryExpressionType.Negate => L.Expression.Negate(Result),
                UnaryExpressionType.BitwiseNot => L.Expression.Not(Result),
                _ => throw new EvaluationException($"Expression of type {expression.Type} is not supported"),
            };
        }

        public override void Visit(ValueExpression expression)
        {
            Result = L.Expression.Constant(expression.Value);
        }

        public override void Visit(Function function)
        {
            var args = new L.Expression[function.Expressions.Length];
            for (int i = 0; i < function.Expressions.Length; i++)
            {
                function.Expressions[i].Accept(this);
                args[i] = Result;
            }

            string functionName = function.Identifier.Name.ToUpperInvariant();
            if (functionName == "IF")
            {
                var numberTypePriority = new Type[] { typeof(double), typeof(float), typeof(long), typeof(int), typeof(short) };
                var index1 = Array.IndexOf(numberTypePriority, args[1].Type);
                var index2 = Array.IndexOf(numberTypePriority, args[2].Type);
                if (index1 >= 0 && index2 >= 0 && index1 != index2)
                {
                    args[1] = L.Expression.Convert(args[1], numberTypePriority[Math.Min(index1, index2)]);
                    args[2] = L.Expression.Convert(args[2], numberTypePriority[Math.Min(index1, index2)]);
                }
                Result = L.Expression.Condition(args[0], args[1], args[2]);
                return;
            }
            else if (functionName == "IN")
            {
                var items = L.Expression.NewArrayInit(args[0].Type,
                        new ArraySegment<L.Expression>(args, 1, args.Length - 1));
                var smi = typeof(Array).GetRuntimeMethod("IndexOf", [typeof(Array), typeof(object)]);
                var r = L.Expression.Call(smi, L.Expression.Convert(items, typeof(Array)), L.Expression.Convert(args[0], typeof(object)));
                Result = L.Expression.GreaterThanOrEqual(r, L.Expression.Constant(0));
                return;
            }

            //Context methods take precedence over built-in functions because they're user-customisable.
            var mi = FindMethod(function.Identifier.Name, args);
            if (mi != null)
            {
                Result = L.Expression.Call(_context, mi.BaseMethodInfo, mi.PreparedArguments);
                return;
            }

            switch (functionName)
            {
                case "MIN":
                    var minArg0 = L.Expression.Convert(args[0], typeof(double));
                    var minArg1 = L.Expression.Convert(args[1], typeof(double));
                    Result = L.Expression.Condition(L.Expression.LessThan(minArg0, minArg1), minArg0, minArg1);
                    break;
                case "MAX":
                    var maxArg0 = L.Expression.Convert(args[0], typeof(double));
                    var maxArg1 = L.Expression.Convert(args[1], typeof(double));
                    Result = L.Expression.Condition(L.Expression.GreaterThan(maxArg0, maxArg1), maxArg0, maxArg1);
                    break;
                case "POW":
                    var powArg0 = L.Expression.Convert(args[0], typeof(double));
                    var powArg1 = L.Expression.Convert(args[1], typeof(double));
                    Result = L.Expression.Power(powArg0, powArg1);
                    break;
                default:
                    throw new MissingMethodException($"method not found: {functionName}");
            }
        }

        public override void Visit(Identifier function)
        {
            if (_context == null)
            {
                Result = L.Expression.Constant(_parameters[function.Name]);
            }
            else
            {
                Result = L.Expression.PropertyOrField(_context, function.Name);
            }
        }

        private ExtendedMethodInfo FindMethod(string methodName, L.Expression[] methodArgs)
        {
            if (_context == null) return null;

            TypeInfo contextTypeInfo = _context.Type.GetTypeInfo();
            TypeInfo objectTypeInfo = typeof(object).GetTypeInfo();
            do
            {
                var methods = contextTypeInfo.DeclaredMethods.Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase) && m.IsPublic && !m.IsStatic);
                var candidates = new List<ExtendedMethodInfo>();
                foreach (var potentialMethod in methods)
                {
                    var methodParams = potentialMethod.GetParameters();
                    var preparedArguments = PrepareMethodArgumentsIfValid(methodParams, methodArgs);

                    if (preparedArguments != null)
                    {
                        var candidate = new ExtendedMethodInfo()
                        {
                            BaseMethodInfo = potentialMethod,
                            PreparedArguments = preparedArguments.Item2,
                            Score = preparedArguments.Item1
                        };
                        if (candidate.Score == 0) return candidate;
                        candidates.Add(candidate);
                    }
                }
                if (candidates.Count != 0) return candidates.OrderBy(method => method.Score).First();
                contextTypeInfo = contextTypeInfo.BaseType.GetTypeInfo();
            } while (contextTypeInfo != objectTypeInfo);
            return null;
        }

        /// <summary>
        /// Returns a tuple where the first item is a score, and the second is a list of prepared arguments. 
        /// Score is a simplified indicator of how close the arguments' types are to the parameters'. A score of 0 indicates a perfect match between arguments and parameters. 
        /// Prepared arguments refers to having the arguments implicitly converted where necessary, and "params" arguments collated into one array.
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private Tuple<int, L.Expression[]> PrepareMethodArgumentsIfValid(ParameterInfo[] parameters, L.Expression[] arguments)
        {
            if (parameters.Length == 0 && arguments.Length == 0) return Tuple.Create(0, arguments);
            if (parameters.Length == 0) return null;

            var lastParameter = parameters.Last();
            bool hasParamsKeyword = lastParameter.IsDefined(typeof(ParamArrayAttribute));
            if (hasParamsKeyword && parameters.Length > arguments.Length) return null;
            L.Expression[] newArguments = new L.Expression[parameters.Length];
            L.Expression[] paramsKeywordArgument = null;
            Type paramsElementType = null;
            int paramsParameterPosition = 0;
            if (!hasParamsKeyword)
            {
                if (parameters.Length != arguments.Length) return null;
            }
            else
            {
                paramsParameterPosition = lastParameter.Position;
                paramsElementType = lastParameter.ParameterType.GetElementType();
                paramsKeywordArgument = new L.Expression[arguments.Length - parameters.Length + 1];
            }

            int functionMemberScore = 0;
            for (int i = 0; i < arguments.Length; i++)
            {
                var isParamsElement = hasParamsKeyword && i >= paramsParameterPosition;
                var argument = arguments[i];
                var argumentType = argument.Type;
                var parameterType = isParamsElement ? paramsElementType : parameters[i].ParameterType;
                if (argumentType != parameterType)
                {
                    bool canCastImplicitly = TryCastImplicitly(argumentType, parameterType, ref argument);
                    if (!canCastImplicitly) return null;
                    functionMemberScore++;
                }
                if (!isParamsElement)
                {
                    newArguments[i] = argument;
                }
                else
                {
                    paramsKeywordArgument[i - paramsParameterPosition] = argument;
                }
            }

            if (hasParamsKeyword)
            {
                newArguments[paramsParameterPosition] = L.Expression.NewArrayInit(paramsElementType, paramsKeywordArgument);
            }
            return Tuple.Create(functionMemberScore, newArguments);
        }

        private bool TryCastImplicitly(Type from, Type to, ref L.Expression argument)
        {
            bool convertingFromPrimitiveType = _implicitPrimitiveConversionTable.TryGetValue(from, out var possibleConversions);
            if (!convertingFromPrimitiveType || !possibleConversions.Contains(to))
            {
                argument = null;
                return false;
            }
            argument = L.Expression.Convert(argument, to);
            return true;
        }

        private L.Expression WithCommonNumericType(L.Expression left, L.Expression right,
            Func<L.Expression, L.Expression, L.Expression> action, BinaryExpressionType expressiontype = BinaryExpressionType.Unknown)
        {
            left = UnwrapNullable(left);
            right = UnwrapNullable(right);

            if (_options.HasFlag(EvaluateOptions.BooleanCalculation))
            {
                if (left.Type == typeof(bool))
                {
                    left = L.Expression.Condition(left, L.Expression.Constant(1.0), L.Expression.Constant(0.0));
                }

                if (right.Type == typeof(bool))
                {
                    right = L.Expression.Condition(right, L.Expression.Constant(1.0), L.Expression.Constant(0.0));
                }
            }

            var precedence = new[]
            {
                typeof(decimal),
                typeof(double),
                typeof(float),
                typeof(ulong),
                typeof(long),
                typeof(uint),
                typeof(int),
                typeof(ushort),
                typeof(short),
                typeof(byte),
                typeof(sbyte)
            };

            int l = Array.IndexOf(precedence, left.Type);
            int r = Array.IndexOf(precedence, right.Type);
            if (l >= 0 && r >= 0)
            {
                var type = precedence[Math.Min(l, r)];
                if (left.Type != type)
                {
                    left = L.Expression.Convert(left, type);
                }

                if (right.Type != type)
                {
                    right = L.Expression.Convert(right, type);
                }
            }
            L.Expression comparer;
            if (IgnoreCaseString)
            {
                if (Ordinal) comparer = L.Expression.Property(null, typeof(StringComparer), nameof(StringComparer.OrdinalIgnoreCase));
                else comparer = L.Expression.Property(null, typeof(StringComparer), nameof(StringComparer.CurrentCultureIgnoreCase));
            }
            else comparer = L.Expression.Property(null, typeof(StringComparer), nameof(StringComparer.Ordinal));

            if (comparer != null && (typeof(string).Equals(left.Type) || typeof(string).Equals(right.Type)))
            {
                switch (expressiontype)
                {
                    case BinaryExpressionType.Equal: return L.Expression.Call(comparer, typeof(StringComparer).GetRuntimeMethod("Equals", [typeof(string), typeof(string)]), [left, right]);
                    case BinaryExpressionType.NotEqual: return L.Expression.Not(L.Expression.Call(comparer, typeof(StringComparer).GetRuntimeMethod("Equals", [typeof(string), typeof(string)]), [left, right]));
                    case BinaryExpressionType.GreaterOrEqual: return L.Expression.GreaterThanOrEqual(L.Expression.Call(comparer, typeof(StringComparer).GetRuntimeMethod("Compare", [typeof(string), typeof(string)]), [left, right]), L.Expression.Constant(0));
                    case BinaryExpressionType.LesserOrEqual: return L.Expression.LessThanOrEqual(L.Expression.Call(comparer, typeof(StringComparer).GetRuntimeMethod("Compare", [typeof(string), typeof(string)]), [left, right]), L.Expression.Constant(0));
                    case BinaryExpressionType.Greater: return L.Expression.GreaterThan(L.Expression.Call(comparer, typeof(StringComparer).GetRuntimeMethod("Compare", [typeof(string), typeof(string)]), [left, right]), L.Expression.Constant(0));
                    case BinaryExpressionType.Lesser: return L.Expression.LessThan(L.Expression.Call(comparer, typeof(StringComparer).GetRuntimeMethod("Compare", [typeof(string), typeof(string)]), [left, right]), L.Expression.Constant(0));
                }
            }
            return action(left, right);
        }

        private static L.Expression UnwrapNullable(L.Expression expression)
        {
            var ti = expression.Type.GetTypeInfo();
            if (ti.IsGenericType && ti.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return L.Expression.Condition(
                    L.Expression.Property(expression, "HasValue"),
                    L.Expression.Property(expression, "Value"),
                    L.Expression.Default(expression.Type.GetTypeInfo().GenericTypeArguments[0]));
            }

            return expression;
        }
    }
}
