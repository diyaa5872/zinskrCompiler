using System;
using System.ComponentModel.Design.Serialization;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace mc
{        
    class Program
    {
        static void Main(string[] args)
        {
            bool showTree=false;
            while(true){
                Console.Write(">");
                var line=Console.ReadLine();
                if(string.IsNullOrWhiteSpace(line)){
                    return ;
                }

                if(line=="#showTree"){
                    showTree=!showTree;
                    Console.WriteLine(showTree? "showing parse trees.": "not showing parse trees");
                    continue;
                }

                var syntaxTree=SyntaxTree.Parse(line);

                if(showTree){
                    var color=Console.ForegroundColor;
                    Console.ForegroundColor=ConsoleColor.DarkGray;
                    PreetyPrint(syntaxTree.Root);
                    Console.ForegroundColor=color;
                }

                if(syntaxTree.Diagnostics.Any()){
                    Console.ForegroundColor=ConsoleColor.DarkRed;

                    foreach(var diagnostics in syntaxTree.Diagnostics){
                        Console.WriteLine(diagnostics);
                    }

                    Console.ForegroundColor=ConsoleColor.White;
                }else{
                    var e = new Evaluator(syntaxTree.Root);
                    var result = e.Evaluate();
                    Console.WriteLine(result);
                }
            }
        }
        static void PreetyPrint(SyntaxNode node, string indent = "",bool isLast=true){
            var marker = isLast ? "'---" : "|---";
                Console.Write(indent);
                Console.Write(marker);
                Console.Write(node.Kind);

                if(node is SyntaxToken t && t.Value!=null){
                    Console.Write(" ");
                    Console.Write(t.Value);
                }

                Console.WriteLine();

                // indent+= "    ";

                indent+=isLast? "  " : "| ";

                var lastChild = node.GetChildren().LastOrDefault();

                foreach(var child in node.GetChildren()){
                    PreetyPrint(child, indent,child==lastChild);
                }
            }
    }
    enum SyntaxKind{
        NumberToken,
        WhitespaceToken,
        PlusToken,
        MinusToken,
        SlashToken,
        StarToken,
        OpenParenthesisToken,
        CloseParenthesisToken,
        ParenthesizedExpression,
        BadToken,
        EndOfFileToken,
        NumberExpression,
        BinaryExpression

    }

    class SyntaxToken : SyntaxNode{
        public SyntaxToken(SyntaxKind kind,int position,string text,object? value){
            Kind=kind;
            Position=position;
            Text=text;
            Value=value;
        }
        public override SyntaxKind Kind { get; }
        public int Position { get; }
        public string? Text { get; }
        public object? Value { get; }

        public override IEnumerable<SyntaxNode> GetChildren(){
            return Enumerable.Empty<SyntaxNode>();
        }
    }

    class Lexer
    {
        private readonly string _text;
        private int _position;
        private List<string> _diagnostics = new List<string>();

        public Lexer(string text){
            _text=text;
            _position=0;
        }

        public IEnumerable<string> Diagonostics => _diagnostics;

        private char Current
        {
            get 
            {
                if (_position >= _text.Length)
                {
                    return '\0';
                }

                return _text[_position];
            }
        }

        private void Next(){
            _position++;
        }

        public SyntaxToken NextToken(){
            //<NUMBERS>
            // + - * /
            //whitespace

            if(_position >= _text.Length){
                return new SyntaxToken(SyntaxKind.EndOfFileToken,_position,"\0",null);
            }

            if(char.IsDigit(Current)){
                var start=_position;

                while(char.IsDigit(Current))
                    Next();

                var length=_position - start;
                var text=_text.Substring(start,length);

                if(!int.TryParse(text,out var value)){
                    _diagnostics.Add($"the number {_text} isn't valid int32");
                    value=0;
                }

                return new SyntaxToken(SyntaxKind.NumberToken,start,text,value);
            }
            if(char.IsWhiteSpace(Current)){
                var start=_position;

                while(char.IsWhiteSpace(Current))
                    Next();

                var length=_position - start;
                var text=_text.Substring(start,length);

                return new SyntaxToken(SyntaxKind.WhitespaceToken,start,text,null);
            }

            if(Current=='+')
                return new SyntaxToken(SyntaxKind.PlusToken,_position++,"+",null);
            else if(Current=='-')
                return new SyntaxToken(SyntaxKind.MinusToken,_position++,"+",null);
            else if(Current=='*')
                return new SyntaxToken(SyntaxKind.StarToken,_position++,"*",null);
            else if(Current=='/')
                return new SyntaxToken(SyntaxKind.SlashToken,_position++,"/",null);
            else if(Current=='(')
                return new SyntaxToken(SyntaxKind.OpenParenthesisToken,_position++,"(",null);
            else if(Current==')')
                return new SyntaxToken(SyntaxKind.CloseParenthesisToken,_position++,")",null);

            _diagnostics.Add($"ERROR: bad character in input: '{Current}'");    

            return new SyntaxToken(SyntaxKind.BadToken,_position++,_text.Substring(_position - 1,1),null);
            
        }
    }


    abstract class SyntaxNode{
        public abstract SyntaxKind Kind { get; }
        public abstract IEnumerable<SyntaxNode> GetChildren();
    }

    abstract class ExpressionSyntax : SyntaxNode{

    }

    sealed class NumberExpressionSyntax : ExpressionSyntax{
        public NumberExpressionSyntax(SyntaxToken numberToken){
                NumberToken = numberToken;
        }

        public override SyntaxKind Kind => SyntaxKind.NumberExpression;
        public SyntaxToken NumberToken { get; }

        public override IEnumerable<SyntaxNode> GetChildren(){
            yield return NumberToken;
        }
    }

    sealed class BinaryExpressionSyntax : ExpressionSyntax {
        public BinaryExpressionSyntax(ExpressionSyntax left, SyntaxToken operatorToken ,ExpressionSyntax right){
            Left = left;
            Right = right;
            OperatorToken = operatorToken;
        }

        public override SyntaxKind Kind => SyntaxKind.BinaryExpression;
        public ExpressionSyntax Left { get; }
        public SyntaxToken OperatorToken { get; }
        public ExpressionSyntax Right { get; }

        public override IEnumerable<SyntaxNode> GetChildren(){
            yield return Left;
            yield  return OperatorToken;
            yield return Right;
        }
    }

    sealed class ParenthesizedExpressionSyntax: ExpressionSyntax{
        public ParenthesizedExpressionSyntax(SyntaxToken openParenthesisToken,ExpressionSyntax expression,SyntaxToken closeParenthesisToken){
            OpenParenthesisToken=openParenthesisToken;
            Expression=expression;
            CloseParenthesisToken=closeParenthesisToken;
        }
        public override SyntaxKind Kind => SyntaxKind.ParenthesizedExpression;
        public SyntaxToken OpenParenthesisToken { get; }
        public ExpressionSyntax Expression { get; }
        public SyntaxToken CloseParenthesisToken { get; }

        public override IEnumerable<SyntaxNode> GetChildren(){
            yield return OpenParenthesisToken;
            yield return CloseParenthesisToken;
            yield return Expression;
        }
    }

    sealed class SyntaxTree{
        public SyntaxTree(IEnumerable<string> diagnostics ,ExpressionSyntax root,SyntaxToken endOffileToken){
            Diagnostics=diagnostics.ToArray();
            Root=root;
            EndOffileToken=endOffileToken;
        }
        public IReadOnlyList<string> Diagnostics  { get; }
        public ExpressionSyntax Root { get; }
        public SyntaxToken EndOffileToken { get; }

        public static SyntaxTree Parse(string text){
            var parser=new Parser(text);
            return parser.Parse();
        }
    }
    
    class Parser{
        private readonly SyntaxToken[] _tokens;
        private List<string>  _diagnostics = new List<string>();
        private int _position;
        public Parser(string text){
            var tokens=new List<SyntaxToken>();
            var lexer=new Lexer(text);
            SyntaxToken token;
            do{
                token = lexer.NextToken();

                if(token.Kind != SyntaxKind.WhitespaceToken && token.Kind != SyntaxKind.BadToken){
                    tokens.Add(token);
                }
            }while(token.Kind != SyntaxKind.EndOfFileToken);

            _tokens = tokens.ToArray();
            _diagnostics.AddRange(lexer.Diagonostics);
        }

        public IEnumerable<string> Diagnostics => _diagnostics;

        private SyntaxToken Peek(int offset){
            var index = _position + offset;
            if(index >= _tokens.Length)
              return _tokens[_tokens.Length - 1];
            
            return _tokens[index];
        }

        private SyntaxToken Current => Peek(0);

        private SyntaxToken NextToken(){
            var current = Current;
            _position++;
            return current;
        }


        private SyntaxToken Match(SyntaxKind kind){
            if(Current.Kind == kind){
                return NextToken();
            }

            _diagnostics.Add($"ERROR : unexpected token <{Current.Kind}> , expected<{kind}> ");
            return new SyntaxToken(kind,Current.Position,"",null);
        }

        private ExpressionSyntax ParseExpression(){
            return ParseTerm();
        }

        public SyntaxTree Parse(){
            var expression = ParseTerm();
            var endOfFileToken=Match(SyntaxKind.EndOfFileToken);
            return new SyntaxTree(_diagnostics,expression,endOfFileToken);
        }

        public ExpressionSyntax ParseTerm(){
            var left = ParseFactor();
            while(Current.Kind == SyntaxKind.PlusToken ||
                  Current.Kind == SyntaxKind.MinusToken )
                  {
                    var  operatorToken=NextToken();
                    var right= ParsePrimaryExpression();
                    left = new BinaryExpressionSyntax(left,operatorToken,right);
                  }
            return left;
        }

        public ExpressionSyntax ParseFactor(){
            var left = ParsePrimaryExpression();
            while(Current.Kind == SyntaxKind.StarToken ||
     
                  Current.Kind == SyntaxKind.SlashToken)
                  {
                    var  operatorToken=NextToken();
                    var right= ParsePrimaryExpression();
                    left = new BinaryExpressionSyntax(left,operatorToken,right);
                  }
            return left;
        }

        private ExpressionSyntax ParsePrimaryExpression(){
            if(Current.Kind==SyntaxKind.OpenParenthesisToken){
                var left=NextToken();
                var expression=ParseExpression();
                var right=Match(SyntaxKind.CloseParenthesisToken);
                return new ParenthesizedExpressionSyntax(left,expression,right);
            }

                var numberToken=Match(SyntaxKind.NumberToken);
                return new NumberExpressionSyntax(numberToken);
        }
    }

    class Evaluator{
        private readonly ExpressionSyntax _root;

        public Evaluator(ExpressionSyntax root){
            this._root = root;
        }

        public int Evaluate(){
            return EvaluateExpression(_root);
        }
        private int EvaluateExpression(ExpressionSyntax node){
            //binary expression
            //numberexpression

            if (node is NumberExpressionSyntax n){
                if (n.NumberToken.Value is int value)
                return value;
            }


            if(node is BinaryExpressionSyntax b){
                var left=EvaluateExpression(b.Left);
                var right=EvaluateExpression(b.Right);

                if(b.OperatorToken.Kind == SyntaxKind.PlusToken)
                       return left + right;
                else if(b.OperatorToken.Kind == SyntaxKind.MinusToken)
                       return left - right;
                else if(b.OperatorToken.Kind == SyntaxKind.StarToken)
                       return left * right;
                else if(b.OperatorToken.Kind == SyntaxKind.SlashToken)
                       return left / right;
                else
                    throw new Exception($"Unexpected Binary operator {b.OperatorToken.Kind}");
                
            }

            if(node is ParenthesizedExpressionSyntax p){
                return EvaluateExpression(p.Expression);
            }

            throw new Exception($"Unexpected node {node.Kind}");

        }
    }
}
