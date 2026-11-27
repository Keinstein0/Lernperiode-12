using newrouter.services;

namespace newrouter.Tests;

public class TaskRoutingServiceTests
{
    [Fact]
    public void RecordAndResolve_ReturnsMappedNodeId()
    {
        var svc = new TaskRoutingService();
        svc.RecordTaskRoute("task-1", "b2-1");

        Assert.Equal("b2-1", svc.ResolveNodeId("task-1"));
        Assert.Null(svc.ResolveNodeId("unknown"));
    }

    [Fact]
    public void RemoveRoutesForNode_OnlyRemovesMatchingNodeRoutes()
    {
        var svc = new TaskRoutingService();
        svc.RecordTaskRoute("t1", "b2-1");
        svc.RecordTaskRoute("t2", "b2-1");
        svc.RecordTaskRoute("t3", "b2-2");

        svc.RemoveRoutesForNode("b2-1");

        Assert.Null(svc.ResolveNodeId("t1"));
        Assert.Null(svc.ResolveNodeId("t2"));
        Assert.Equal("b2-2", svc.ResolveNodeId("t3"));
    }
}
