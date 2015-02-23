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
    Assign, InfixOp, Not,
    Ampersand, Ephemeral,
    At, Question, DontCare, Ellipsis,
    Intrinsic,
    Use,
    Type, Interface, Trait, Primitive, Class, Actor,
    Object,
    As, Is, Isnt,
    Var, Let, New, Fun, Be,
    Capability,
    This,
    Return, Break, Continue, Error,
    Consume, Recover,
    If, Then, Else, ElseIf, End,
    While, Do, Repeat, Until, For, In,
    Match, Where, Try, With
  }
}
