using System;

namespace NCalc.Domain
{
    public class ValueExpression : LogicalExpression
    {

        public ValueExpression() { }

        public ValueExpression(object value, ValueType type)
        {
            Value = value;
            Type = type;
        }

        public ValueExpression(object value)
        {
            Type = value.GetTypeCode() switch
            {
                TypeCode.Boolean => ValueType.Boolean,
                TypeCode.DateTime => ValueType.DateTime,
                TypeCode.Decimal or TypeCode.Double or TypeCode.Single => ValueType.Float,
                TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 => ValueType.Integer,
                TypeCode.String => ValueType.String,
                _ => throw new EvaluationException("This value could not be handled: " + value),
            };
            Value = value;
        }

        public ValueExpression(string value)
        {
            Value = value;
            Type = ValueType.String;
        }

        public ValueExpression(int value)
        {
            Value = value;
            Type = ValueType.Integer;
        }

        public ValueExpression(float value)
        {
            Value = value;
            Type = ValueType.Float;
        }

        public ValueExpression(DateTime value)
        {
            Value = value;
            Type = ValueType.DateTime;
        }

        public ValueExpression(bool value)
        {
            Value = value;
            Type = ValueType.Boolean;
        }

        public object Value { get; set; }
        public ValueType Type { get; set; }

        public override void Accept(LogicalExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }

    public enum ValueType
    {
        Integer,
        String,
        DateTime,
        Float,
        Boolean
    }
}
