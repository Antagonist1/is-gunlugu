using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class FirstRunFlowTests
{
    [Fact]
    public void Starts_OnWelcome_WithNowhereToGoBack()
    {
        var flow = new FirstRunFlow();

        Assert.Equal(FirstRunStep.Welcome, flow.Step);
        Assert.False(flow.CanGoBack);
        Assert.False(flow.IsReturning);
    }

    [Fact]
    public void QuickStart_GoesThroughFileThenDrive()
    {
        var flow = new FirstRunFlow();

        flow.QuickStart();
        Assert.Equal(FirstRunStep.FirstFile, flow.Step);

        flow.FileChosen();
        Assert.Equal(FirstRunStep.Drive, flow.Step);
        Assert.False(flow.IsReturning);
    }

    [Fact]
    public void Back_RetracesTheNewUserPath()
    {
        var flow = new FirstRunFlow();
        flow.QuickStart();
        flow.FileChosen();

        flow.Back();
        Assert.Equal(FirstRunStep.FirstFile, flow.Step);

        flow.Back();
        Assert.Equal(FirstRunStep.Welcome, flow.Step);
    }

    [Fact]
    public void Returning_AsksWhereFrom_ThenFetches()
    {
        var flow = new FirstRunFlow();

        flow.Returning();
        Assert.Equal(FirstRunStep.Source, flow.Step);
        Assert.True(flow.IsReturning);

        flow.FetchFromDrive();
        Assert.Equal(FirstRunStep.Fetch, flow.Step);
        Assert.True(flow.IsReturning);
    }

    [Fact]
    public void Back_RetracesTheReturningPath_AndForgetsItWasReturning()
    {
        var flow = new FirstRunFlow();
        flow.Returning();
        flow.FetchFromDrive();

        flow.Back();
        Assert.Equal(FirstRunStep.Source, flow.Step);

        flow.Back();
        Assert.Equal(FirstRunStep.Welcome, flow.Step);
        Assert.False(flow.IsReturning);
    }

    [Fact]
    public void DriveTurnedOutEmpty_StartFresh_JoinsTheNewUserPath()
    {
        var flow = new FirstRunFlow();
        flow.Returning();
        flow.FetchFromDrive();

        flow.StartFresh();

        Assert.Equal(FirstRunStep.FirstFile, flow.Step);
        Assert.False(flow.IsReturning);
    }

    [Fact]
    public void Back_OnWelcome_StaysThere()
    {
        var flow = new FirstRunFlow();

        flow.Back();

        Assert.Equal(FirstRunStep.Welcome, flow.Step);
    }
}
