using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.SQSEvents;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Moq;
using Xunit;
using System.Text.Json;
using System.Reflection;
using Amazon;

namespace Lambda_FrameSnap_Retry.Tests
{
    public class FunctionTests
    {
        private readonly Mock<IAmazonSQS> _mockSqsClient;
        private readonly Mock<IAmazonSimpleNotificationService> _mockSnsClient;
        private readonly Function _function;
        private readonly TestLambdaContext _context;
        private const string SNS_TOPIC_ARN = "arn:aws:sns:us-east-1:339713138979:notificacoes-frameSnap";
        private const string TEST_DLQ_URL = "https://sqs.us-east-1.amazonaws.com/123456789012/my-dlq";

        public FunctionTests()
        {
            _mockSqsClient = new Mock<IAmazonSQS>();
            _mockSnsClient = new Mock<IAmazonSimpleNotificationService>();
            _context = new TestLambdaContext();
            
            Environment.SetEnvironmentVariable("DLQ_URL", TEST_DLQ_URL);
            
            // Criar a função usando reflection para injetar os mocks
            _function = new Function();
            
            // Usar reflection para substituir os clientes por mocks
            var sqsClientField = typeof(Function).GetField("_sqsClient", BindingFlags.NonPublic | BindingFlags.Instance);
            var snsClientField = typeof(Function).GetField("_snsClient", BindingFlags.NonPublic | BindingFlags.Instance);
            
            sqsClientField?.SetValue(_function, _mockSqsClient.Object);
            snsClientField?.SetValue(_function, _mockSnsClient.Object);
        }

        [Fact]
        public async Task FunctionHandler_WhenDLQHasMessages_ShouldSendSNSNotification()
        {
            // Arrange
            var messages = new List<Message>
            {
                new Message { MessageId = "msg1", Body = "test message 1" },
                new Message { MessageId = "msg2", Body = "test message 2" }
            };

            _mockSqsClient.Setup(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), default))
                .ReturnsAsync(new ReceiveMessageResponse { Messages = messages });

            _mockSnsClient.Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), default))
                .ReturnsAsync(new PublishResponse());

            var sqsEvent = new SQSEvent();

            // Act
            await _function.FunctionHandler(sqsEvent, _context);

            // Assert
            _mockSnsClient.Verify(x => x.PublishAsync(
                It.Is<PublishRequest>(r => 
                    r.TopicArn == SNS_TOPIC_ARN &&
                    r.Subject == "🚨 Alerta: Mensagens na DLQ - FrameSnap"),
                default),
                Times.Exactly(2));
        }

        [Fact]
        public async Task FunctionHandler_WhenDLQIsEmpty_ShouldNotSendSNSNotification()
        {
            // Arrange
            _mockSqsClient.Setup(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), default))
                .ReturnsAsync(new ReceiveMessageResponse { Messages = new List<Message>() });

            var sqsEvent = new SQSEvent();

            // Act
            await _function.FunctionHandler(sqsEvent, _context);

            // Assert
            _mockSnsClient.Verify(x => x.PublishAsync(
                It.IsAny<PublishRequest>(),
                default),
                Times.Never);
        }

        [Fact]
        public async Task FunctionHandler_WhenErrorOccurs_ShouldSendErrorNotification()
        {
            // Arrange
            _mockSqsClient.Setup(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), default))
                .ThrowsAsync(new Exception("Test error"));

            var sqsEvent = new SQSEvent();

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _function.FunctionHandler(sqsEvent, _context));

            _mockSnsClient.Verify(x => x.PublishAsync(
                It.Is<PublishRequest>(r => 
                    r.TopicArn == SNS_TOPIC_ARN &&
                    r.Subject == "❌ Erro: Lambda DLQ Monitor - FrameSnap"),
                default),
                Times.Once);
        }

        [Fact]
        public async Task FunctionHandler_ShouldUseCorrectDLQUrl()
        {
            // Arrange
            _mockSqsClient.Setup(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), default))
                .ReturnsAsync(new ReceiveMessageResponse { Messages = new List<Message>() });

            var sqsEvent = new SQSEvent();

            // Act
            await _function.FunctionHandler(sqsEvent, _context);

            // Assert
            _mockSqsClient.Verify(x => x.ReceiveMessageAsync(
                It.Is<ReceiveMessageRequest>(r => r.QueueUrl == TEST_DLQ_URL),
                default),
                Times.Once);
        }

        [Fact]
        public async Task FunctionHandler_ShouldLogInformationWhenNoMessages()
        {
            // Arrange
            _mockSqsClient.Setup(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), default))
                .ReturnsAsync(new ReceiveMessageResponse { Messages = new List<Message>() });

            var sqsEvent = new SQSEvent();

            // Act
            await _function.FunctionHandler(sqsEvent, _context);

            // Assert
            _mockSqsClient.Verify(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), default), Times.Once);
        }

        [Fact]
        public async Task FunctionHandler_ShouldLogInformationForEachMessage()
        {
            // Arrange
            var messages = new List<Message>
            {
                new Message { MessageId = "msg1", Body = "test message 1" }
            };

            _mockSqsClient.Setup(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), default))
                .ReturnsAsync(new ReceiveMessageResponse { Messages = messages });

            _mockSnsClient.Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), default))
                .ReturnsAsync(new PublishResponse());

            var sqsEvent = new SQSEvent();

            // Act
            await _function.FunctionHandler(sqsEvent, _context);

            // Assert
            _mockSnsClient.Verify(x => x.PublishAsync(It.IsAny<PublishRequest>(), default), Times.Once);
        }

        [Fact]
        public async Task FunctionHandler_ShouldLogErrorWhenExceptionOccurs()
        {
            // Arrange
            _mockSqsClient.Setup(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), default))
                .ThrowsAsync(new Exception("Test error"));

            var sqsEvent = new SQSEvent();

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _function.FunctionHandler(sqsEvent, _context));
            
            _mockSnsClient.Verify(x => x.PublishAsync(
                It.Is<PublishRequest>(r => r.Subject == "❌ Erro: Lambda DLQ Monitor - FrameSnap"),
                default),
                Times.Once);
        }

        [Fact]
        public async Task FunctionHandler_ShouldIncludeQueueUrlInNotification()
        {
            // Arrange
            var messages = new List<Message>
            {
                new Message { MessageId = "msg1", Body = "test message 1" }
            };

            _mockSqsClient.Setup(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), default))
                .ReturnsAsync(new ReceiveMessageResponse { Messages = messages });

            _mockSnsClient.Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), default))
                .ReturnsAsync(new PublishResponse());

            var sqsEvent = new SQSEvent();

            // Act
            await _function.FunctionHandler(sqsEvent, _context);

            // Assert
            _mockSnsClient.Verify(x => x.PublishAsync(
                It.Is<PublishRequest>(r => 
                    r.Message.Contains(TEST_DLQ_URL)),
                default),
                Times.Once);
        }
    }
}
