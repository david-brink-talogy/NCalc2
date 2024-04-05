using Antlr4.Runtime;

namespace NCalc
{
    internal class SyntaxError<T>(T offendingSymbol, int line, int charPositionInLine, string message, RecognitionException exception)
    {
        public T OffendingSymbol = offendingSymbol;
        public int Line = line;
        public int CharPositionInLine = charPositionInLine;
        public string Message = message;
        public RecognitionException Exception = exception;

        public override string ToString() => $"{Message}:{Line}:{CharPositionInLine}";
    }
}
