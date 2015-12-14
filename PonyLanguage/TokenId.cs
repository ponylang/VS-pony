namespace Pony
{
  public enum TokenId
  {
    Ignore,
    Comment,
    TrueFalse, String, Number,
    TypeID, VarID,
    LBrace, RBrace,
    LParen, RParen,
    LSquare, RSquare,
    Comma, Dot, Tilde, Colon, Semi,
    Arrow, DoubleArrow,
    Assign, InfixOp,
    Ampersand, Ephemeral, Borrowed,
    At, Question, DontCare, Ellipsis,
    Intrinsic,
    Use,
    Type, Interface, Trait, Primitive, Struct, Class, Actor,
    Object, Lambda, Delegate,
    As, Is, Isnt,
    Var, Let, Embed, New, Fun, Be,
    Capability,
    This,
    Return, Break, Continue, Error, CompileError,
    Consume, Recover,
    If, Ifdef, Then, Else, ElseIf, End,
    While, Do, Repeat, Until, For, In,
    Match, Where, Try, With,
    Not, And, Or, Xor,
    IdentityOf, AddressOf
  }
}
