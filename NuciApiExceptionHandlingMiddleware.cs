using System;
using System.Collections.Generic;
using System.Net;
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
            catch (Exception exception) when (
                exception is AuthenticationException ||
                exception is UnauthorizedAccessException
            )
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.Unauthorized,
                    NuciApiErrorResponse.Unauthorised);
            }
            catch (AuthenticationException exception)
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.Forbidden,
                    new NuciApiErrorResponse(exception));
            }
            catch (BadHttpRequestException exception)
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.BadRequest,
                    new NuciApiErrorResponse(exception));
            }
            catch (KeyNotFoundException)
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.NotFound,
                    NuciApiErrorResponse.NotFound);
            }
            catch (DuplicateEntityException)
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.Conflict,
                    NuciApiErrorResponse.AlreadyExists);
            }
            catch (TimeoutException)
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.GatewayTimeout,
                    NuciApiErrorResponse.Timeout);
            }
            catch (NotImplementedException exception)
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.NotImplemented,
                    new NuciApiErrorResponse(
                        exception.Message ?? "This endpoint has not been implemented."));
            }
            catch (Exception exception)
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.InternalServerError,
                    new NuciApiErrorResponse(exception));
            }
        }

        private async Task WriteErrorResponseAsync(
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