﻿using Parlot.Fluent;
using SqlParser.Core.Syntax;
using System.Collections.Generic;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace SqlParser.Core.Statements
{
    /*
     * selectStatement ::= SELECT DISTINCT? topExpression? columnsList FROM tableNames | SELECT valuesList
     * 
     * topExpression ::= TOP(number)
     *
     * columnsList ::= * | columnName (, columnName)*
     * 
     * columnName ::= ((identifier '.')? identifier | functionExpression) (AS alias)?
     * 
     * valuesList ::= value (, value)*
     * 
     * value ::= (expression | functionExpression) (AS alias)?
     * 
     * functionExpression ::= identifier '(' functionParameters ')'
     * 
     * functionParameters :: functionParameter (, functionParameter)*
     * 
     * functionParameter :: expression | (identifier '.')? identifier
     * 
     * tablesList ::= tableName (, tableName)*
     * 
     * tableName ::= identifier (AS alias)?
     * 
     * alias ::= identifier | string
     */
    public class SelectStatement : Statement
    {
        internal protected static readonly Parser<string> Select = Terms.Text("SELECT", caseInsensitive: true);
        internal protected static readonly Parser<string> Distinct = Terms.Text("DISTINCT", caseInsensitive: true);
        internal protected static readonly Parser<string> Top = Terms.Text("TOP", caseInsensitive: true);
        internal protected static readonly Parser<string> From = Terms.Text("FROM", caseInsensitive: true);
        internal protected static readonly Parser<string> As = Terms.Text("AS", caseInsensitive: true);

        public static readonly Deferred<Statement> Statement = Deferred<Statement>();

        static SelectStatement()
        {
            var number = SqlParser.Number
               .Then(e => new SyntaxNode(new SyntaxToken
               {
                   Kind = SyntaxKind.NumberToken,
                   Value = e
               }));
            var identifier = SqlParser.Identifier
                .Then(e => new SyntaxNode(new SyntaxToken
                {
                    Kind = SyntaxKind.IdentifierToken,
                    Value = e.ToString()
                }));
            var stringLiteral = SqlParser.StringLiteral
                .Then(e => new SyntaxNode(new SyntaxToken
                {
                    Kind = SyntaxKind.StringToken,
                    Value = e.ToString()
                }));
            var alias = identifier.Or(stringLiteral);
            var expression = new SqlParser().Expression;
            var functionParaemeter = ZeroOrOne(identifier.And(SqlParser.Dot))
                .And(identifier
                    .And(ZeroOrOne(As.And(alias))))
                .Then(e =>
                {
                    var paramNode = new SyntaxNode(new SyntaxToken
                    {
                        Kind = SyntaxKind.IdentifierToken,
                        Value = e.Item3.Item1.Token.Value
                    });
                    var prevParamNode = paramNode;
                    if (e.Item1 != null)
                    {
                        paramNode = new SyntaxNode(new SyntaxToken
                        {
                            Kind = SyntaxKind.DotToken,
                            Value = e.Item2
                        });

                        paramNode.ChildNodes.Add(e.Item1);
                        paramNode.ChildNodes.Add(prevParamNode);
                    }

                    if (e.Item3.Item2.Item1 != null)
                    {
                        paramNode = new SyntaxNode(new SyntaxToken
                        {
                            Kind = SyntaxKind.AsKeyword,
                            Value = e.Item3.Item2.Item1
                        });

                        paramNode.ChildNodes.Add(prevParamNode);
                        paramNode.ChildNodes.Add(e.Item3.Item2.Item2);
                    }

                    return paramNode;
                })
                .Or(expression);
            var functionExpression = identifier
                .And(Between(SqlParser.OpenParen, Separated(SqlParser.Comma, functionParaemeter), SqlParser.CloseParen)
                    .Then(e =>
                    {
                        for (int i = 1; i < e.Count; i += 2)
                        {
                            e.Insert(i, (new SyntaxNode(new SyntaxToken
                            {
                                Kind = SyntaxKind.CommaToken,
                                Value = ","
                            })));
                        }

                        return e;
                    }))
                .Then(e =>
                {
                    e.Item1.ChildNodes.Add(new SyntaxNode(new SyntaxToken
                    {
                        Kind = SyntaxKind.OpenParenthesisToken,
                        Value = '('
                    }));

                    foreach (var node in e.Item2)
                    {
                        e.Item1.ChildNodes.Add(node);
                    }

                    e.Item1.ChildNodes.Add(new SyntaxNode(new SyntaxToken
                    {
                        Kind = SyntaxKind.CloseParenthesisToken,
                        Value = ')'
                    }));

                    return e.Item1;
                });
            var column = functionExpression
                .Or(ZeroOrOne(identifier.And(SqlParser.Dot))
                .And(identifier)
                    .Then(e =>
                    {
                        var columnNode = new SyntaxNode(new SyntaxToken
                        {
                            Kind = SyntaxKind.IdentifierToken,
                            Value = e.Item3.Token.Value
                        });
                        var prevColumnNode = columnNode;
                        if (e.Item1 != null)
                        {
                            columnNode = new SyntaxNode(new SyntaxToken
                            {
                                Kind = SyntaxKind.DotToken,
                                Value = e.Item2
                            });

                            columnNode.ChildNodes.Add(e.Item1);
                            columnNode.ChildNodes.Add(prevColumnNode);
                        }

                        return columnNode;
                    }))
                .And(ZeroOrOne(As.And(alias))
                    .Then(e =>
                    {
                        if (e.Item2 == null)
                        {
                            return null;
                        }
                        
                        var aliasNode = new SyntaxNode(new SyntaxToken
                        {
                            Kind = SyntaxKind.AsKeyword,
                            Value = e.Item1
                        });
                        aliasNode.ChildNodes.Add(e.Item2);

                        return aliasNode;
                    }))
                .Then(e =>
                {
                    if (e.Item2 == null)
                    {
                        return e.Item1;
                    }
                    else
                    {
                        e.Item2.ChildNodes.Insert(0, e.Item1);

                        return e.Item2;
                    }
                });
            var columnsList = SqlParser.Asterisk
                .Then(e =>
                {
                    var columnNode = new SyntaxNode(new SyntaxToken
                    {
                        Kind = SyntaxKind.AsteriskToken,
                        Value = e
                    });

                    return new List<SyntaxNode> { columnNode };
                })
                .Or(Separated(SqlParser.Comma, column))
                .Then(e =>
                {
                    for (int i = 1; i < e.Count; i += 2)
                    {
                        e.Insert(i, (new SyntaxNode(new SyntaxToken
                        {
                            Kind = SyntaxKind.CommaToken,
                            Value = ","
                        })));
                    }

                    return e;
                });
            var value = functionExpression.Or(expression)
                .And(ZeroOrOne(As.And(alias))).Then(e =>
                {
                    var valueNode = e.Item1;
                    if (e.Item2.Item1 != null)
                    {
                        valueNode.ChildNodes.Add(new SyntaxNode(new SyntaxToken
                        {
                            Kind = SyntaxKind.AsKeyword,
                            Value = e.Item2.Item1
                        }));
                        valueNode.ChildNodes.Add(e.Item2.Item2);
                    }

                    return valueNode;
                });
            var valuesList = Separated(SqlParser.Comma, value)
            .Then(e =>
            {
                for (int i = 1; i < e.Count; i += 2)
                {
                    e.Insert(i, (new SyntaxNode(new SyntaxToken
                    {
                        Kind = SyntaxKind.CommaToken,
                        Value = ","
                    })));
                }

                return e;
            });
            var table = identifier.And(ZeroOrOne(As
                .Then(e => new SyntaxNode(new SyntaxToken
                {
                    Kind = SyntaxKind.AsKeyword,
                    Value = e
                })).And(alias)))
                .Then(e =>
                {
                    var tableNode = new SyntaxNode(new SyntaxToken());
                    var tableName = e.Item1.Token.Value.ToString();

                    tableNode.ChildNodes.Add(new SyntaxNode(new SyntaxToken
                    {
                        Kind = SyntaxKind.IdentifierToken,
                        Value = tableName
                    }));

                    if (e.Item2.Item2 != null)
                    {
                        tableName = e.Item2.Item2.Token.Value.ToString();

                        tableNode.ChildNodes.Add(new SyntaxNode(new SyntaxToken
                        {
                            Kind = SyntaxKind.AsKeyword,
                            Value = e.Item2.Item1.Token.Value
                        }));

                        if (e.Item2.Item2.Token.Kind == SyntaxKind.StringToken)
                        {
                            tableNode.ChildNodes.Add(new SyntaxNode(new SyntaxToken
                            {
                                Kind = SyntaxKind.StringToken,
                                Value = tableName
                            }));
                        }
                        else
                        {
                            tableNode.ChildNodes.Add(new SyntaxNode(new SyntaxToken
                            {
                                Kind = SyntaxKind.IdentifierToken,
                                Value = tableName
                            }));
                        }
                    }

                    return tableNode;
                });
            var tablesList = Separated(SqlParser.Comma, table)
                .Then(e =>
                {
                    for (int i = 1; i < e.Count; i += 2)
                    {
                        e.Insert(i, (new SyntaxNode(new SyntaxToken
                        {
                            Kind = SyntaxKind.CommaToken,
                            Value = ","
                        })));
                    }

                    return e;
                });
            var topExpression = ZeroOrOne(Top
                .Then(e => new SyntaxNode(new SyntaxToken
                {
                    Kind = SyntaxKind.TopKeyword,
                    Value = e
                }))
                .And(Between(SqlParser.OpenParen, number, SqlParser.CloseParen)))
                .Then(e => new List<SyntaxNode>
                    {
                        e.Item1,
                        new SyntaxNode(new SyntaxToken
                        {
                            Kind = SyntaxKind.OpenParenthesisToken,
                            Value = '('
                        }),
                        e.Item2,
                        new SyntaxNode(new SyntaxToken
                        {
                            Kind = SyntaxKind.CloseParenthesisToken,
                            Value = ')'
                        })
                    });
            var selectAndFromClauses = Select
                .Then(e => new SyntaxNode(new SyntaxToken
                {
                    Kind = SyntaxKind.SelectKeyword,
                    Value = e
                }))
                .And(ZeroOrOne(Distinct
                    .Then(e => new SyntaxNode(new SyntaxToken
                    {
                        Kind = SyntaxKind.DistinctKeyword,
                        Value = e
                    }))))
                .And(topExpression)
                .And(columnsList)
                .And(From
                    .Then(e => new SyntaxNode(new SyntaxToken
                    {
                        Kind = SyntaxKind.FromKeyword,
                        Value = e
                    })))
                .And(tablesList);
            var selectClause = Select
                .Then(e => new SyntaxNode(new SyntaxToken
                {
                    Kind = SyntaxKind.SelectKeyword,
                    Value = e
                })).And(valuesList);
            var selectStatement = selectAndFromClauses.Or(selectClause
                .Then(e => (e.Item1, (SyntaxNode)null, (List<SyntaxNode>)null, e.Item2, (SyntaxNode)null, (List<SyntaxNode>)null)));

            Statement.Parser = selectStatement.Then<Statement>(e =>
                {
                    if (e.Item6 == null)
                    {
                        var values = e.Item4
                            .Where(n => n.Token.Kind != SyntaxKind.CommaToken)
                            .Select(e => e.Token.Value.ToString())
                            .ToList();
                        // Avoid select clause values to contain FROM
                        if (values.Contains("FROM"))
                        {
                            return null;
                        }

                        var valueAliases = e.Item4
                            .Where(n => n.Token.Kind != SyntaxKind.CommaToken && n.ChildNodes.Any(c => c.Kind == SyntaxKind.AsKeyword))
                            .Select(e => e.ChildNodes[e.ChildNodes.Count - 1].Token.Value.ToString())
                            .ToList();
                        var statement = new SelectStatement(string.Empty)
                        {
                            TableAliases = Enumerable.Empty<string>(),
                            TableNames = Enumerable.Empty<string>(),
                            ColumnAliases = Enumerable.Empty<string>(),
                            ColumnNames = Enumerable.Empty<string>()
                        };
                        var selectClause = new SyntaxNode(new SyntaxToken { Kind = SyntaxKind.SelectClause });

                        selectClause.ChildNodes.Add(e.Item1);


                        foreach (var node in e.Item4)
                        {
                            selectClause.ChildNodes.Add(node);
                        }

                        statement.Nodes.Add(selectClause);

                        return statement;
                    }
                    else
                    {
                        var tableNames = e.Item6
                            .Where(n => n.Token.Kind != SyntaxKind.CommaToken)
                            .Select(e => e.ChildNodes[0].Token.Value.ToString())
                            .ToList();
                        var tableAliases = e.Item6
                            .Where(n => n.Token.Kind != SyntaxKind.CommaToken && n.ChildNodes.Any(c => c.Kind == SyntaxKind.AsKeyword))
                            .Select(e => e.ChildNodes[e.ChildNodes.Count - 1].Token.Value.ToString())
                            .ToList();
                        var columnNames = e.Item4
                            .Where(n => n.Token.Kind != SyntaxKind.CommaToken)
                            .Select(e =>
                            {
                                if (e.ChildNodes.Any())
                                {
                                    if (e.ChildNodes[0].Kind == SyntaxKind.AsteriskToken)
                                    {
                                        return e.ChildNodes[0].Token.Value.ToString();
                                    }
                                    else if (e.ChildNodes[0].Kind == SyntaxKind.AsKeyword)
                                    {
                                        if (e.ChildNodes[0].ChildNodes[0].ChildNodes.Any())
                                        {
                                            return e.ChildNodes[0].ChildNodes[0].ChildNodes[1].Token.Value.ToString();
                                        }
                                        else
                                        {
                                            return e.ChildNodes[0].ChildNodes[0].Token.Value.ToString();
                                        }
                                    }
                                    else if (e.ChildNodes[0].Kind == SyntaxKind.DotToken)
                                    {
                                        return e.ChildNodes[0].ChildNodes[1].Token.Value.ToString();
                                    }
                                    else
                                    {
                                        return e.ChildNodes[1].Token.Value.ToString();
                                    }
                                }
                                else
                                {
                                    return e.Token.Value.ToString();
                                }
                            })
                            .ToList();
                        var columnAliases = e.Item4
                            .Where(n => n.Token.Kind != SyntaxKind.CommaToken && n.Kind == SyntaxKind.AsKeyword)
                            .Select(e => e.ChildNodes[1].Token.Value.ToString())
                            .ToList();
                        var statement = new SelectStatement(tableNames[0])
                        {
                            TableAliases = tableAliases,
                            TableNames = tableNames,
                            ColumnAliases = columnAliases,
                            ColumnNames = columnNames
                        };
                        var selectClause = new SyntaxNode(new SyntaxToken { Kind = SyntaxKind.SelectClause });
                        var fromClause = new SyntaxNode(new SyntaxToken { Kind = SyntaxKind.FromClause });

                        selectClause.ChildNodes.Add(e.Item1);

                        if (e.Item2 != null)
                        {
                            selectClause.ChildNodes.Add(e.Item2);
                        }

                        if (e.Item3[0] != null)
                        {
                            foreach (var node in e.Item3)
                            {
                                selectClause.ChildNodes.Add(node);
                            }
                        }

                        foreach (var node in e.Item4)
                        {
                            selectClause.ChildNodes.Add(node);
                        }

                        fromClause.ChildNodes.Add(e.Item5);

                        foreach (var node in e.Item6)
                        {
                            fromClause.ChildNodes.Add(node);
                        }

                        statement.Nodes.Add(selectClause);
                        statement.Nodes.Add(fromClause);

                        return statement;
                    }
                });
        }

        public SelectStatement(string tableName) : base(tableName)
        {

        }

        public IEnumerable<string> ColumnAliases { get; private set; }

        public IEnumerable<string> ColumnNames { get; private set; }

        public IEnumerable<string> TableAliases { get; private set; }

        public IEnumerable<string> TableNames { get; private set; }
    }
}
