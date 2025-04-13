using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using System.Text.Json;
using Amazon;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Lambda_FrameSnap_Retry;

public class Function
{
    private readonly IAmazonSQS _sqsClient;
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly string _dlqUrl;
    private const string SNS_TOPIC_ARN = "arn:aws:sns:us-east-1:339713138979:notificacoes-frameSnap";
    private const string AWS_REGION = "us-east-1";

    /// <summary>
    /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
    /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
    /// region the Lambda function is executed in.
    /// </summary>
    public Function()
    {
        var region = RegionEndpoint.GetBySystemName(AWS_REGION);
        _sqsClient = new AmazonSQSClient(region);
        _snsClient = new AmazonSimpleNotificationServiceClient(region);
        _dlqUrl = Environment.GetEnvironmentVariable("DLQ_URL") ?? throw new InvalidOperationException("DLQ_URL environment variable is not set");
    }

    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an SQS event object and can be used 
    /// to respond to SQS messages.
    /// </summary>
    /// <param name="sqsEvent">The event for the Lambda function handler to process.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        try
        {
            var receiveMessageRequest = new ReceiveMessageRequest
            {
                QueueUrl = _dlqUrl,
                MaxNumberOfMessages = 10,
                WaitTimeSeconds = 20
            };

            var response = await _sqsClient.ReceiveMessageAsync(receiveMessageRequest);

            if (response.Messages.Any())
            {
                foreach (var message in response.Messages)
                {
                    var notificationMessage = new
                    {
                        MessageId = message.MessageId,
                        Body = message.Body,
                        Timestamp = DateTime.UtcNow,
                        Error = "Mensagem encontrada na DLQ",
                        QueueUrl = _dlqUrl
                    };

                    var publishRequest = new PublishRequest
                    {
                        TopicArn = SNS_TOPIC_ARN,
                        Message = JsonSerializer.Serialize(notificationMessage),
                        Subject = "🚨 Alerta: Mensagens na DLQ - FrameSnap"
                    };

                    await _snsClient.PublishAsync(publishRequest);
                    context.Logger.LogInformation($"Notificação enviada para mensagem {message.MessageId}");
                }
            }
            else
            {
                context.Logger.LogInformation("Nenhuma mensagem encontrada na DLQ");
            }
        }
        catch (Exception ex)
        {
            var errorMessage = new
            {
                Error = "Erro ao processar DLQ",
                Message = ex.Message,
                StackTrace = ex.StackTrace,
                Timestamp = DateTime.UtcNow
            };

            var publishRequest = new PublishRequest
            {
                TopicArn = SNS_TOPIC_ARN,
                Message = JsonSerializer.Serialize(errorMessage),
                Subject = "❌ Erro: Lambda DLQ Monitor - FrameSnap"
            };

            await _snsClient.PublishAsync(publishRequest);
            context.Logger.LogError($"Erro ao processar DLQ: {ex.Message}");
            throw;
        }
    }
}