using LabelGenerator.Core.Models;
using LabelGenerator.Core.Services.DataSources;

namespace LabelGenerator.Core.Tests;

public sealed class DatabaseSqlBuilderTests
{
    [Fact]
    public void BuildPrimaryQuery_UsesSqlServerTop()
    {
        var profile = new DataSourceProfile
        {
            ProviderInvariantName = "Microsoft.Data.SqlClient",
            PrimaryView = "dbo.vw_label_candidates",
            MaxRows = 25
        };

        var sql = DatabaseSqlBuilder.BuildPrimaryQuery(profile);

        Assert.Equal("select top (25) * from dbo.vw_label_candidates", sql);
    }

    [Fact]
    public void BuildPrimaryQuery_RejectsUnsafeViewName()
    {
        var profile = new DataSourceProfile
        {
            ProviderInvariantName = "Microsoft.Data.SqlClient",
            PrimaryView = "dbo.vw_label_candidates; drop table x",
            MaxRows = 25
        };

        Assert.Throws<ArgumentException>(() => DatabaseSqlBuilder.BuildPrimaryQuery(profile));
    }

    [Fact]
    public void BuildDetailQuery_UsesDapperListExpansion()
    {
        var profile = new DataSourceProfile
        {
            DetailView = "dbo.vw_label_details",
            KeyColumn = "ProductCode"
        };

        var sql = DatabaseSqlBuilder.BuildDetailQuery(profile, ["@p0", "@p1"]);

        Assert.Equal("select * from dbo.vw_label_details where ProductCode in (@p0, @p1)", sql);
    }

    [Fact]
    public void BuildPrimaryQuery_AllowsSelectStatement()
    {
        var profile = new DataSourceProfile
        {
            PrimarySql = "select ProductCode, Status from dbo.Items where IsActive = 1"
        };

        var sql = DatabaseSqlBuilder.BuildPrimaryQuery(profile);

        Assert.Equal("select ProductCode, Status from dbo.Items where IsActive = 1", sql);
    }

    [Fact]
    public void BuildPrimaryQuery_RejectsNonSelectStatement()
    {
        var profile = new DataSourceProfile
        {
            PrimarySql = "delete from dbo.Items"
        };

        Assert.Throws<ArgumentException>(() => DatabaseSqlBuilder.BuildPrimaryQuery(profile));
    }

    [Fact]
    public void BuildDetailQuery_ReplacesKeysTokenForCustomSelect()
    {
        var profile = new DataSourceProfile
        {
            DetailSql = "select * from dbo.Items where ProductCode in ({Keys})"
        };

        var sql = DatabaseSqlBuilder.BuildDetailQuery(profile, ["@p0"]);

        Assert.Equal("select * from dbo.Items where ProductCode in (@p0)", sql);
    }

    [Fact]
    public void BuildKeyParameterPlaceholders_UsesQuestionMarksForOdbc()
    {
        var profile = new DataSourceProfile
        {
            ProviderInvariantName = "System.Data.Odbc"
        };

        var placeholders = DatabaseSqlBuilder.BuildKeyParameterPlaceholders(profile, 3);

        Assert.Equal(["?", "?", "?"], placeholders);
    }

    [Fact]
    public void BuildLookupQuery_ReplacesScanToken()
    {
        var profile = new DataSourceProfile
        {
            LookupSql = "select ProductCode from BarcodeLookup where Barcode = {Scan}"
        };

        var sql = DatabaseSqlBuilder.BuildLookupQuery(profile, "@scan");

        Assert.Equal("select ProductCode from BarcodeLookup where Barcode = @scan", sql);
    }

    [Fact]
    public void BuildLookupQuery_RequiresScanToken()
    {
        var profile = new DataSourceProfile
        {
            LookupSql = "select ProductCode from BarcodeLookup"
        };

        Assert.Throws<ArgumentException>(() => DatabaseSqlBuilder.BuildLookupQuery(profile, "@scan"));
    }

    [Fact]
    public void BuildScanParameterPlaceholder_UsesQuestionMarkForOdbc()
    {
        var profile = new DataSourceProfile
        {
            ProviderInvariantName = "System.Data.Odbc"
        };

        var placeholder = DatabaseSqlBuilder.BuildScanParameterPlaceholder(profile);

        Assert.Equal("?", placeholder);
    }
}
