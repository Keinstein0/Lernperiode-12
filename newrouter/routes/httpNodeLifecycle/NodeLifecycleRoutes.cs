using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace newrouter.routes.httpNodeLifecycle
{
    public static class NodeLifecycleRoutes
    {
        public static IEndpointRouteBuilder MapItemEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/routes");

            group.MapPost("/register", Register);
            group.MapPost("/heartbeat", Heartbeat);

            return app;
        }

        private static IResult Register()
        {
            return TypedResults.Ok();
        }

        private static IResult Heartbeat()
        {
            return TypedResults.Ok();
        }
    }
}