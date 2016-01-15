// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Testing;
using Xunit;

namespace Microsoft.AspNetCore.Mvc
{
    public class RequireHttpsAttributeTests
    {
        [Fact]
        public void OnAuthorization_AllowsTheRequestIfItIsHttps()
        {
            // Arrange
            var requestContext = new DefaultHttpContext();
            requestContext.Request.Scheme = "https";

            var authContext = CreateAuthorizationContext(requestContext);
            var attr = new RequireHttpsAttribute();

            // Act
            attr.OnAuthorization(authContext);

            // Assert
            Assert.Null(authContext.Result);
        }

        public static IEnumerable<object[]> RedirectToHttpEndpointTestData
        {
            get
            {
                // host, pathbase, path, query, expectedRedirectUrl
                var data = new TheoryData<string, string, string, string, string>();

                data.Add("localhost", null, null, null, "https://localhost");
                data.Add("localhost:5000", null, null, null, "https://localhost:5000");
                data.Add("localhost", "/pathbase", null, null, "https://localhost/pathbase");
                data.Add("localhost", "/pathbase", "/path", null, "https://localhost/pathbase/path");
                data.Add("localhost", "/pathbase", "/path", "?foo=bar", "https://localhost/pathbase/path?foo=bar");

                // Encode some special characters on the url.
                // Two paths hit aspnet/External#50 with Mono on Mac.
                if (!TestPlatformHelper.IsMac || !TestPlatformHelper.IsMono)
                {
                    data.Add("localhost", "/path?base", null, null, "https://localhost/path%3Fbase");
                    data.Add("localhost", null, "/pa?th", null, "https://localhost/pa%3Fth");
                }

                data.Add("localhost", "/", null, "?foo=bar%2Fbaz", "https://localhost/?foo=bar%2Fbaz");

                // Urls with punycode
                // 本地主機 is "localhost" in chinese traditional, "xn--tiq21tzznx7c" is the
                // punycode representation.
                data.Add("本地主機", "/", null, null, "https://xn--tiq21tzznx7c/");
                return data;
            }
        }

        [Theory]
        [MemberData(nameof(RedirectToHttpEndpointTestData))]
        public void OnAuthorization_RedirectsToHttpsEndpoint_ForNonHttpsGetRequests(
            string host,
            string pathBase,
            string path,
            string queryString,
            string expectedUrl)
        {
            // Arrange
            var requestContext = new DefaultHttpContext();
            requestContext.Request.Scheme = "http";
            requestContext.Request.Method = "GET";
            requestContext.Request.Host = HostString.FromUriComponent(host);

            if (pathBase != null)
            {
                requestContext.Request.PathBase = new PathString(pathBase);
            }

            if (path != null)
            {
                requestContext.Request.Path = new PathString(path);
            }

            if (queryString != null)
            {
                requestContext.Request.QueryString = new QueryString(queryString);
            }

            var authContext = CreateAuthorizationContext(requestContext);
            var attr = new RequireHttpsAttribute();

            // Act
            attr.OnAuthorization(authContext);

            // Assert
            Assert.NotNull(authContext.Result);
            var result = Assert.IsType<RedirectResult>(authContext.Result);

            Assert.True(result.Permanent);
            Assert.Equal(expectedUrl, result.Url);
        }

        [Theory]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("PATCH")]
        [InlineData("DELETE")]
        public void OnAuthorization_SignalsBadRequestStatusCode_ForNonHttpsAndNonGetRequests(string method)
        {
            // Arrange
            var requestContext = new DefaultHttpContext();
            requestContext.Request.Scheme = "http";
            requestContext.Request.Method = method;
            var authContext = CreateAuthorizationContext(requestContext);
            var attr = new RequireHttpsAttribute();

            // Act
            attr.OnAuthorization(authContext);

            // Assert
            Assert.NotNull(authContext.Result);
            var result = Assert.IsType<HttpStatusCodeResult>(authContext.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        }

        [Fact]
        public void HandleNonHttpsRequestExtensibility()
        {
            // Arrange
            var requestContext = new DefaultHttpContext();
            requestContext.Request.Scheme = "http";

            var authContext = CreateAuthorizationContext(requestContext);
            var attr = new CustomRequireHttpsAttribute();

            // Act
            attr.OnAuthorization(authContext);

            // Assert
            var result = Assert.IsType<HttpStatusCodeResult>(authContext.Result);
            Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        }

        private class CustomRequireHttpsAttribute : RequireHttpsAttribute
        {
            protected override void HandleNonHttpsRequest(AuthorizationFilterContext filterContext)
            {
                filterContext.Result = new HttpStatusCodeResult(StatusCodes.Status404NotFound);
            }
        }

        private static AuthorizationFilterContext CreateAuthorizationContext(HttpContext ctx)
        {
            var actionContext = new ActionContext(ctx, new RouteData(), new ActionDescriptor());
            return new AuthorizationFilterContext(actionContext, new IFilterMetadata[0]);
        }
    }
}