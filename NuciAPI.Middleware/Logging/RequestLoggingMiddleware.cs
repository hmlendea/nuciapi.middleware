using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NuciLog.Core;
using NuciWeb.HTTP;

namespace NuciAPI.Middleware.Logging
{
    internal sealed class RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger logger) : NuciApiMiddleware(next)
    {
        public override async Task InvokeAsync(HttpContext context)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            string ipAddress = GetClientIpAddress(context);

            IEnumerable<LogInfo> logInfos =
            [
                new LogInfo(MyLogInfoKey.Method, context.Request.Method),
                new LogInfo(MyLogInfoKey.Path, context.Request.Path),
                new LogInfo(MyLogInfoKey.QueryString, context.Request.QueryString.ToString()),
                new LogInfo(MyLogInfoKey.IpAddress, ipAddress),
                new LogInfo(MyLogInfoKey.Hostname, string.Join(',', NetworkUtils.GetHostnames(ipAddress))),
                new LogInfo(MyLogInfoKey.ClientId, TryGetHeaderValue(context, NuciApiHeaderNames.ClientId)),
                new LogInfo(MyLogInfoKey.RequestId, TryGetHeaderValue(context, NuciApiHeaderNames.RequestId)),
                new LogInfo(MyLogInfoKey.Timestamp, TryGetHeaderValue(context, NuciApiHeaderNames.Timestamp)),
                new LogInfo(MyLogInfoKey.HmacToken, TryGetHeaderValue(context, NuciApiHeaderNames.HmacToken))
            ];

            logger.Info(
                MyOperation.HttpRequest,
                OperationStatus.Started,
                logInfos);

            try
            {
                await Next(context);
            }
            catch (Exception ex)
            {
                logger.Error(
                    MyOperation.HttpRequest,
                    OperationStatus.Failure,
                    ex,
                    logInfos);

                throw;
            }

            int statusCode = context.Response.StatusCode;
            stopwatch.Stop();

            logInfos = logInfos
                .Append(new LogInfo(MyLogInfoKey.StatusCode, statusCode))
                .Append(new LogInfo(MyLogInfoKey.ElapsedMilliseconds, stopwatch.ElapsedMilliseconds));

            if (statusCode >= 200 && statusCode < 300)
            {
                logger.Info(
                    MyOperation.HttpRequest,
                    OperationStatus.Success,
                    logInfos);
            }
            else
            {
                logger.Error(
                    MyOperation.HttpRequest,
                    OperationStatus.Failure,
                    logInfos);
            }
        }
    }
}