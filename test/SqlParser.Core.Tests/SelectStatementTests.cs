﻿using Parlot;
using SqlParser.Core;
using SqlParser.Core.Statements;
using System.Linq;
using Xunit;

namespace SqlParser.Tests
{
    public class SelectStatementTests
    {
        [Theory]
        [InlineData("Select * From People", new[] { "People" }, new[] { "*" })]
        [InlineData("Select FirstName From People", new[] { "People" }, new[] { "FirstName" })]
        [InlineData("Select FirstName, LastName From People", new[] { "People" }, new[] { "FirstName", "LastName" })]
        [InlineData("select * from People, Contacts", new[] { "People", "Contacts" }, new[] { "*" })]
        [InlineData("select People.FirstName, Contatcs.Address from People, Contacts", new[] { "People", "Contacts" }, new[] { "People.FirstName", "Contatcs.Address" })]
        public void ParseSelectStatement(string text, string[] expectedTableNames, string[] expectedColumnNames)
        {
            // Arrange
            var context = new SqlContext(text);
            var result = new ParseResult<Statement>();

            // Act
            SelectStatement.Statement.Parse(context, ref result);

            // Assert
            var statement = result.Value as SelectStatement;
            Assert.Equal(4, statement.Tokens.Count());
            Assert.Equal("SELECT", statement.Tokens.ElementAt(0).Value);
            Assert.Equal("FROM", statement.Tokens.ElementAt(2).Value);
            Assert.Equal(expectedTableNames, statement.TableNames.ToArray());
            Assert.Equal(expectedColumnNames, statement.ColumnNames);
        }

        [Theory]
        [InlineData("Select People.FirstName From People", new string[] { "People.FirstName" })]
        [InlineData("Select People.FirstName, People.LastName From People", new string[] { "People.FirstName", "People.LastName" })]
        public void ParseFullQualifiedColumnNames(string text, string[] expectedColumnNames)
        {
            // Arrange
            var context = new SqlContext(text);
            var result = new ParseResult<Statement>();

            // Act
            SelectStatement.Statement.Parse(context, ref result);

            // Assert
            var statement = result.Value as SelectStatement;
            Assert.Equal(expectedColumnNames, statement.ColumnNames.ToArray());
        }
    }
}
