using MediatR;
using MicroRabbit.Banking.Domain.Commands;
using MicroRabbit.Banking.Domain.Events;
using MicroRabbit.Domain.Core.Bus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroRabbit.Banking.Domain.CommandHandler
{
    public class TransferCommandHandler : IRequestHandler<CreateTransferCommand, bool>
    {
        //publishing to RabbitMQ
        private readonly IEventBus _bus;

        public TransferCommandHandler(IEventBus bus)
        {
            _bus = bus;
        }
        public Task<bool> Handle(CreateTransferCommand request, CancellationToken cancellationToken)
        {
            //publish an event
            _bus.Publish(new TransferCreatedEvent(request.From, request.To, request.Amount));

            return Task.FromResult(true);
        }
    }
}
