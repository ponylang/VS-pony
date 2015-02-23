using System;
using System.Collections.Generic;


namespace Pony
{
  class Lexer
  {
    private string _buffer;
    private int _state;
    private readonly IDictionary<string, TokenId> _keywords = new Dictionary<string, TokenId>();
    private readonly IList<Tuple<string, TokenId>> _symbols = new List<Tuple<string, TokenId>>();

    public const int newState = -2;
    private const int tripleStringState = -1;
    public const int normalState = 0;

    public Lexer()
    {
      _keywords["_"] = TokenId.DontCare;
      _keywords["true"] = TokenId.TrueFalse;
      _keywords["false"] = TokenId.TrueFalse;
      _keywords["compiler_intrinsic"] = TokenId.Intrinsic;
      _keywords["use"] = TokenId.Use;
      _keywords["type"] = TokenId.Type;
      _keywords["interface"] = TokenId.Interface;
      _keywords["trait"] = TokenId.Trait;
      _keywords["primitive"] = TokenId.Primitive;
      _keywords["class"] = TokenId.Class;
      _keywords["actor"] = TokenId.Actor;
      _keywords["object"] = TokenId.Object;
      _keywords["as"] = TokenId.As;
      _keywords["is"] = TokenId.Is;
      _keywords["isnt"] = TokenId.Isnt;
      _keywords["var"] = TokenId.Var;
      _keywords["let"] = TokenId.Let;
      _keywords["new"] = TokenId.New;
      _keywords["fun"] = TokenId.Fun;
      _keywords["be"] = TokenId.Be;
      _keywords["iso"] = TokenId.Capability;
      _keywords["trn"] = TokenId.Capability;
      _keywords["val"] = TokenId.Capability;
      _keywords["ref"] = TokenId.Capability;
      _keywords["box"] = TokenId.Capability;
      _keywords["tag"] = TokenId.Capability;
      _keywords["this"] = TokenId.This;
      _keywords["return"] = TokenId.Return;
      _keywords["break"] = TokenId.Break;
      _keywords["continue"] = TokenId.Continue;
      _keywords["error"] = TokenId.Error;
      _keywords["consume"] = TokenId.Continue;
      _keywords["recover"] = TokenId.Recover;
      _keywords["if"] = TokenId.If;
      _keywords["then"] = TokenId.Then;
      _keywords["else"] = TokenId.Else;
      _keywords["elseif"] = TokenId.ElseIf;
      _keywords["end"] = TokenId.End;
      _keywords["while"] = TokenId.While;
      _keywords["do"] = TokenId.Do;
      _keywords["repeat"] = TokenId.Repeat;
      _keywords["until"] = TokenId.Until;
      _keywords["for"] = TokenId.For;
      _keywords["in"] = TokenId.In;
      _keywords["match"] = TokenId.Match;
      _keywords["where"] = TokenId.Where;
      _keywords["try"] = TokenId.Try;
      _keywords["with"] = TokenId.With;

      // Longer symbols must appear before shorter prefixes
      _symbols.Add(new Tuple<string, TokenId>("...", TokenId.Ellipsis));
      _symbols.Add(new Tuple<string, TokenId>("->", TokenId.Arrow));
      _symbols.Add(new Tuple<string, TokenId>("=>", TokenId.DoubleArrow));
      _symbols.Add(new Tuple<string, TokenId>("<<", TokenId.InfixOp));
      _symbols.Add(new Tuple<string, TokenId>(">>", TokenId.InfixOp));
      _symbols.Add(new Tuple<string, TokenId>("==", TokenId.InfixOp));
      _symbols.Add(new Tuple<string, TokenId>("!=", TokenId.InfixOp));
      _symbols.Add(new Tuple<string, TokenId>("<=", TokenId.InfixOp));
      _symbols.Add(new Tuple<string, TokenId>(">=", TokenId.InfixOp));
      _symbols.Add(new Tuple<string, TokenId>("{", TokenId.LBrace));
      _symbols.Add(new Tuple<string, TokenId>("}", TokenId.RBrace));
      _symbols.Add(new Tuple<string, TokenId>("(", TokenId.LParen));
      _symbols.Add(new Tuple<string, TokenId>(")", TokenId.RParen));
      _symbols.Add(new Tuple<string, TokenId>("[", TokenId.LSquare));
      _symbols.Add(new Tuple<string, TokenId>("]", TokenId.RSquare));
      _symbols.Add(new Tuple<string, TokenId>(",", TokenId.Comma));
      _symbols.Add(new Tuple<string, TokenId>(".", TokenId.Dot));
      _symbols.Add(new Tuple<string, TokenId>("~", TokenId.Tilde));
      _symbols.Add(new Tuple<string, TokenId>(":", TokenId.Colon));
      _symbols.Add(new Tuple<string, TokenId>(";", TokenId.Semi));
      _symbols.Add(new Tuple<string, TokenId>("=", TokenId.Assign));
      _symbols.Add(new Tuple<string, TokenId>("+", TokenId.InfixOp));
      _symbols.Add(new Tuple<string, TokenId>("-", TokenId.InfixOp));
      _symbols.Add(new Tuple<string, TokenId>("*", TokenId.InfixOp));
      _symbols.Add(new Tuple<string, TokenId>("%", TokenId.InfixOp));
      _symbols.Add(new Tuple<string, TokenId>("@", TokenId.At));
      _symbols.Add(new Tuple<string, TokenId>("<", TokenId.InfixOp));
      _symbols.Add(new Tuple<string, TokenId>(">", TokenId.InfixOp));
      _symbols.Add(new Tuple<string, TokenId>("|", TokenId.InfixOp));
      _symbols.Add(new Tuple<string, TokenId>("&", TokenId.Ampersand));
      _symbols.Add(new Tuple<string, TokenId>("^", TokenId.Ephemeral));
      _symbols.Add(new Tuple<string, TokenId>("?", TokenId.Question));
    }


    public void HaveLine(string buffer, int start_state)
    {
      _buffer = buffer;
      _state = start_state;
    }


    public TokenId Token(out int length)
    {
      TokenId r;

      switch(_state)
      {
        case tripleStringState:
          r = ProcessTripleString(out length);
          break;

        case normalState:
          r = NextToken(out length);
          break;

        default:
          r = ProcessBlockComment(out length);
          break;
      }

      _buffer = _buffer.Remove(0, length);
      return r;
    }


    public int EndState()
    {
      int length;

      while(_buffer.Length > 0) // Ignore tokens until we run out of text
        Token(out length);

      return _state;
    }


    private TokenId NextToken(out int length)
    {
      char c = _buffer[0];

      if(c == '/')
        return ProcessSlash(out length);

      if(c == '"')
        return ProcessString(out length);

      if(c >= '0' && c <= '9')
        return ProcessNumber(out length);

      if((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_')
        return ProcessId(out length);

      if(IsSymbolChar(c))
        return ProcessSymbol(out length);

      length = 1;
      return TokenId.Ignore;
    }


    // Numbers

    private TokenId ProcessNumber(out int length)
    {
      if(_buffer[0] == '0')
      {
        if(_buffer.Length == 1)
        {
          length = 1;
          return TokenId.Number;
        }

        switch(_buffer[1])
        {
          case 'x':
          case 'X':
            length = NumberLength(2, 16);
            return TokenId.Number;

          case 'b':
          case 'B':
            length = NumberLength(2, 2);
            return TokenId.Number;
        }
      }

      // We have either a decimal integer or a float
      length = DecimalLength();
      return TokenId.Number;
    }


    private int DecimalLength()
    {
      // We've already had one digit
      int length = NumberLength(1, 10);

      if(_buffer.Length > (length + 1) && _buffer[length] == '.')
      {
        // Integer part is followed by a dot, might be a float
        if(_buffer[length + 1] >= '0' && _buffer[length + 1] <= '9')
        {
          // Float with mantissa
          length++; // For .
          length = NumberLength(length, 10);  // Mantissa
        }
      }

      if(_buffer.Length > length && (_buffer[length] == 'e' || _buffer[length] == 'E'))
      {
        // Float with exponent
        length++; // For e

        if(_buffer.Length > length && _buffer[length] == '-')
          length++; // For exponent -

        length = NumberLength(length, 10);  // Exponent
      }

      return length;
    }


    private int NumberLength(int start_offset, int int_base)
    {
      for(int length = start_offset; length < _buffer.Length; length++)
      {
        char c = _buffer[length];
        int digit = int_base;

        if((c >= '0') && (c <= '9'))
          digit = c - '0';
        else if((c >= 'a') && (c <= 'z'))
          digit = c - 'a' + 10;
        else if((c >= 'A') && (c <= 'Z'))
          digit = c - 'A' + 10;

        if(digit >= int_base)
          return length;
      }

      return _buffer.Length;
    }


    // Strings

    private TokenId ProcessString(out int length)
    {
      if(_buffer.Length >= 3 && _buffer[1] == '"' && _buffer[2] == '"')
      {
        // Triple quoted string
        length = 3;
        _state = tripleStringState;
        return TokenId.String;
      }

      // Normal string
      for(int len = 1; len < _buffer.Length; len++)
      {
        if(_buffer[len] == '"')
        {
          length = len + 1;
          return TokenId.String;
        }

        if(_buffer[len] == '\\') // Escape, skip next character
          len++;
      }

      length = _buffer.Length;
      return TokenId.String;
    }


    private TokenId ProcessTripleString(out int length)
    {
      for(int len = 3; len < _buffer.Length; len++)
      {
        if(_buffer[len - 2] == '"' && _buffer[len - 1] == '"' && _buffer[len] == '"')
        {
          length = len;
          _state = normalState;
          return TokenId.String;
        }
      }

      length = _buffer.Length;
      return TokenId.String;
    }


    // Identifiers

    private TokenId ProcessId(out int length)
    {
      length = IdLength();
      string id = _buffer.Substring(0, length);

      // Check for keywords
      if(_keywords.ContainsKey(id))
        return _keywords[id];

      if((_buffer[0] >= 'A' && _buffer[0] <= 'Z') || (_buffer[0] == '_' && _buffer[1] >= 'A' && _buffer[1] <= 'Z'))
        return TokenId.TypeID;

      return TokenId.VarID;
    }


    private int IdLength()
    {
      for(int length = 1; length < _buffer.Length; length++)
        if(!IsIdChar(_buffer[length]))
          return length;

      return _buffer.Length;
    }


    private static bool IsIdChar(char c)
    {
      return ((c >= 'a') && (c <= 'z'))
        || ((c >= 'A') && (c <= 'Z'))
        || ((c >= '0') && (c <= '9'))
        || (c == '_') || (c == '\'');
    }


    // Symbols

    private TokenId ProcessSymbol(out int length)
    {
      foreach(var s in _symbols)
      {
        if(_buffer.StartsWith(s.Item1))
        {
          length = s.Item1.Length;
          return s.Item2;
        }
      }

      // Not a valid symbol
      length = 1;
      return TokenId.Ignore;
    }


    private static bool IsSymbolChar(char c)
    {
      return ((c >= '!') && (c <= '.'))
        || ((c >= ':') && (c <= '@'))
        || ((c >= '[') && (c <= '^'))
        || ((c >= '{') && (c <= '~'));
    }


    // Comments

    private TokenId ProcessSlash(out int length)
    {
      if(_buffer.Length > 1 && _buffer[1] == '*')
      {
        // Block comment
        length = 2;
        _state = 1;
        return TokenId.Comment;
      }

      if(_buffer.Length > 1 && _buffer[1] == '/')
      {
        // Line comment
        length = _buffer.Length;
        return TokenId.Comment;
      }

      // Just a slash
      length = 1;
      return TokenId.InfixOp;
    }


    private TokenId ProcessBlockComment(out int length)
    {
      for(int len = 1; len < _buffer.Length; len++)
      {
        if(_buffer[len - 1] == '/' && _buffer[len] == '*')
        {
          // Increase nesting
          _state++;
          len++;
        }
        else if(_buffer[len - 1] == '*' && _buffer[len] == '/')
        {
          // Decrease nesting
          _state--;
          len++;

          if(_state == 0)
          {
            // End of comment
            length = len;
            return TokenId.Comment;
          }
        }
      }

      length = _buffer.Length;
      return TokenId.Comment;
    }
  }
}
