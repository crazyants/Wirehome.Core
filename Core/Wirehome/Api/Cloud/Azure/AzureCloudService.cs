﻿using System;
using System.Threading.Tasks;
using Wirehome.Contracts.Api;
using Wirehome.Contracts.Components;
using Wirehome.Contracts.Logging;
using Wirehome.Contracts.Services;
using Wirehome.Contracts.Settings;
using Newtonsoft.Json.Linq;

namespace Wirehome.Api.Cloud.Azure
{
    public class AzureCloudService : ServiceBase, IApiAdapter
    {
        private const string NamespaceName = "ha4iot";

        private readonly IApiDispatcherService _apiService;
        private readonly ISettingsService _settingsService;
        private readonly ILogger _log;

        private QueueSender _outboundQueue;
        private QueueReceiver _inboundQueue;

        public AzureCloudService(IApiDispatcherService apiService, ISettingsService settingsService, ILogService logService)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _log = logService?.CreatePublisher(nameof(AzureCloudService)) ?? throw new ArgumentNullException(nameof(logService));
        }

        public event EventHandler<ApiRequestReceivedEventArgs> ApiRequestReceived;

        public override void Startup()
        {
            _apiService.RegisterAdapter(this);

            var settings = _settingsService.GetSettings<AzureCloudServiceSettings>();
            if (!settings.IsEnabled || !string.IsNullOrEmpty(settings.ControllerId))
            {
                return;
            }

            SetupOutboundQueue(settings);
            SetupInboundQueue(settings);
        }

        private void SetupOutboundQueue(AzureCloudServiceSettings settings)
        {
            var options = new QueueSenderOptions
            {
                NamespaceName = NamespaceName,
                QueueName = "outbound-" + settings.ControllerId,
                Authorization = settings.OutboundQueueAuthorization
            };

            _outboundQueue = new QueueSender(options, _log);
        }

        private void SetupInboundQueue(AzureCloudServiceSettings settings)
        {
            var options = new QueueReceiverOptions
            {
                NamespaceName = NamespaceName,
                QueueName = "inbound-" + settings.ControllerId,
                Authorization = settings.InboundQueueAuthorization
            };

            _inboundQueue = new QueueReceiver(options, _log);
            _inboundQueue.MessageReceived += DistpachMessage;
            _inboundQueue.Enable();
        }

        public void NotifyStateChanged(IComponent component)
        {
        }

        private void DistpachMessage(object sender, MessageReceivedEventArgs e)
        {
            var apiRequest = e.Body.ToObject<ApiRequest>();

            var correlationId = (string)e.Body["CorrelationId"];
            
            var context = new QueueBasedApiContext(correlationId, apiRequest.Action, apiRequest.Parameter, apiRequest.ResultHash);
            var eventArgs = new ApiRequestReceivedEventArgs(context);
            ApiRequestReceived?.Invoke(this, eventArgs);

            if (!eventArgs.IsHandled)
            {
                context.ResultCode = ApiResultCode.ActionNotSupported;
            }

            SendResponseMessage(context).Wait();
        }

        private async Task SendResponseMessage(QueueBasedApiContext context)
        {
            var clientEtag = (string)context.Parameter["ETag"];

            var brokerProperties = new JObject
            {
                ["CorrelationId"] = context.CorrelationId
            };

            var message = new JObject
            {
                ["ResultCode"] = context.ResultCode.ToString(),
                ["Content"] = context.Result
            };

            var serverEtag = (string)context.Result["Meta"]["Hash"];
            message["ETag"] = serverEtag;

            if (!string.Equals(clientEtag, serverEtag))
            {
                message["Content"] = context.Result;
            }

            await _outboundQueue.SendAsync(brokerProperties, message);
        }
    }
}