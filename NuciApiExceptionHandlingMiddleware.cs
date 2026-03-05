using System;
using System.Collections.Generic;
using System.Net;
using System.Security;
using System.Security.Authentication;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NuciAPI.Responses;
using NuciDAL.Repositories;

namespace NuciAPI.Middleware
{
    public sealed class NuciApiExceptionHandlingMiddleware(
        RequestDelegate next) : NuciApiMiddleware(next)
    {
        public override async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await Next(context);
            }
            catch (Exception ex) when (
                ex is SecurityException ||
                ex is UnauthorizedAccessException
            )
            {
                await WriteResponseAsync(
                    context,
                    HttpStatusCode.Unauthorized,
                    new NuciApiErrorResponse(ex));
            }
            catch (AuthenticationException ex)
            {
                await WriteResponseAsync(
                    context,
                    HttpStatusCode.Forbidden,
                    new NuciApiErrorResponse(ex));
            }
            catch (KeyNotFoundException)
            {
                await WriteResponseAsync(
                    context,
                    HttpStatusCode.NotFound,
                    NuciApiErrorResponse.NotFound);
            }
            catch (DuplicateEntityException)
            {
                await WriteResponseAsync(
                    context,
                    HttpStatusCode.Conflict,
                    NuciApiErrorResponse.AlreadyExists);
            }
            catch (TimeoutException)
            {
                await WriteResponseAsync(
                    context,
                    HttpStatusCode.GatewayTimeout,
                    new NuciApiErrorResponse("The request has timed out."));
            }
            catch (NotImplementedException ex)
            {
                await WriteResponseAsync(
                    context,
                    HttpStatusCode.NotImplemented,
                    new NuciApiErrorResponse(
                        ex.Message ?? "This endpoint has not been implemented."));
            }
            catch (Exception ex)
            {
                await WriteResponseAsync(
                    context,
                    HttpStatusCode.BadRequest,
                    new NuciApiErrorResponse(ex));
            }
        }

        private async Task WriteResponseAsync(
            HttpContext context,
            HttpStatusCode statusCode,
            NuciApiErrorResponse errorResponse)
        {
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(errorResponse);
        }
    }
}