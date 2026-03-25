using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Security.Authentication;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NuciAPI.Responses;
using NuciDAL.Repositories;

namespace NuciAPI.Middleware.ExceptionHandling
{
    internal sealed class ExceptionHandlingMiddleware(
        RequestDelegate next) : NuciApiMiddleware(next)
    {
        public override async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await Next(context);
            }
            catch (Exception exception) when (
                exception is BadHttpRequestException ||
                exception is FormatException ||
                exception is ArgumentException ||
                exception is ValidationException)
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.BadRequest,
                    new NuciApiErrorResponse(
                        exception.Message,
                        NuciApiResponseCodes.ErrorCodes.BadRequest));
            }
            catch (Exception exception) when (
                exception is SecurityException ||
                exception is UnauthorizedAccessException)
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.Forbidden,
                    NuciApiErrorResponse.Unauthorised);
            }
            catch (Exception exception) when (
                exception is HttpRequestException ||
                exception is TaskCanceledException ||
                exception is TimeoutException)
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.ServiceUnavailable,
                    NuciApiErrorResponse.ServiceDependencyUnavailable);
            }
            catch (AuthenticationException)
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.Unauthorized,
                    NuciApiErrorResponse.AuthenticationFailure);
            }
            catch (Exception exception) when (
                exception is EntityNotFoundException ||
                exception is KeyNotFoundException)
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.NotFound,
                    NuciApiErrorResponse.NotFound);
            }
            catch (Exception exception) when (
                exception is EntityNotFoundException ||
                exception is RequestAlreadyProcessedException)
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.Conflict,
                    NuciApiErrorResponse.AlreadyExists);
            }
            catch (RequestAlreadyProcessedException exception)
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.Conflict,
                    new NuciApiErrorResponse(
                        exception.Message,
                        NuciApiResponseCodes.ErrorCodes.AlreadyProcessed));
            }
            catch (NotImplementedException exception)
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.NotImplemented,
                    new NuciApiErrorResponse(
                        exception.Message ?? NuciApiResponseMessages.ErrorMessages.NotImplemented,
                        NuciApiResponseCodes.ErrorCodes.NotImplemented));
            }
            catch (OperationCanceledException)
            {
                await WriteErrorResponseAsync(
                    context,
                    StatusCodes.Status499ClientClosedRequest,
                    NuciApiErrorResponse.ClientClosedTheRequest);
            }
            catch
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.InternalServerError,
                    NuciApiErrorResponse.InternalServerError);
            }
        }

        private async Task WriteErrorResponseAsync(
            HttpContext context,
            HttpStatusCode statusCode,
            NuciApiErrorResponse errorResponse)
            => await WriteErrorResponseAsync(context, (int)statusCode, errorResponse);

        private async Task WriteErrorResponseAsync(
            HttpContext context,
            int statusCode,
            NuciApiErrorResponse errorResponse)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(errorResponse);
        }
    }
}