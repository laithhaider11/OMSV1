using MediatR;
using OMSV1.Application.Commands.Expenses;
using OMSV1.Domain.Entities.Expenses;
using OMSV1.Domain.Entities.Attachments; // Assuming AttachmentCU is defined here.
using OMSV1.Domain.Enums;
using OMSV1.Domain.SeedWork;
using OMSV1.Infrastructure.Interfaces; // For IPhotoService.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OMSV1.Application.Handlers.Expenses
{
    public class AddDailyExpensesCommandHandler : IRequestHandler<AddDailyExpensesCommand, Guid>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPhotoService _photoService;

        public AddDailyExpensesCommandHandler(IUnitOfWork unitOfWork, IPhotoService photoService)
        {
            _unitOfWork = unitOfWork;
            _photoService = photoService;
        }

        public async Task<Guid> Handle(AddDailyExpensesCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // 1. Retrieve the associated MonthlyExpenses entity.
                var monthlyExpenses = await _unitOfWork.Repository<MonthlyExpenses>()
                    .GetByIdAsync(request.MonthlyExpensesId);

                if (monthlyExpenses == null)
                {
                    throw new KeyNotFoundException($"MonthlyExpenses with ID {request.MonthlyExpensesId} not found.");
                }

                if (monthlyExpenses.Status != Status.New && monthlyExpenses.Status != Status.ReturnedToSupervisor)
                {
                    throw new InvalidOperationException("Cannot add daily expenses to a MonthlyExpenses that is not in Pending status.");
                }

                // 2. Create the DailyExpenses object.
                var dailyExpense = new DailyExpenses(
                    request.Price,
                    request.Quantity,
                    request.Notes,
                    DateTime.SpecifyKind(request.ExpenseDate, DateTimeKind.Utc),
                    request.ExpenseTypeId,
                    request.MonthlyExpensesId
                );

                // 3. Add any subexpenses.
                if (request.SubExpenses != null && request.SubExpenses.Count > 0)
                {
                    foreach (var item in request.SubExpenses)
                    {
                        dailyExpense.AddSubExpense(item.Price, item.Quantity, item.Notes, item.ExpenseTypeId);
                    }
                }

                // 4. Add the DailyExpenses entity to the repository.
                await _unitOfWork.Repository<DailyExpenses>().AddAsync(dailyExpense);

                // 5. Get the total amount of the daily expense
                var totalAmount = dailyExpense.GetTotalAmount();

                // 6. Associate the DailyExpense with the MonthlyExpenses and adjust its total amount
                monthlyExpenses.AddDailyExpense(dailyExpense, await _unitOfWork.Repository<Threshold>().GetAllAsync());
                monthlyExpenses.AdjustTotalAmount(totalAmount);

                // 7. If a receipt file was provided, validate and process it.
                if (request.Receipt != null)
                {
                    if (request.Receipt.Length == 0)
                    {
                        throw new ArgumentException("Uploaded receipt file is empty.");
                    }

                    // Upload the file using the photo service.
                    var photoResult = await _photoService.AddPhotoAsync(request.Receipt, dailyExpense.Id, EntityType.Expense);

                    // Create the attachment entity using the uploaded file path.
                    var attachment = new AttachmentCU(
                        filePath: photoResult.FilePath,
                        entityType: EntityType.Expense,
                        entityId: dailyExpense.Id
                    );

                    // Add the attachment to its repository.
                    await _unitOfWork.Repository<AttachmentCU>().AddAsync(attachment);
                }

                // 8. Update the MonthlyExpenses entity.
                await _unitOfWork.Repository<MonthlyExpenses>().UpdateAsync(monthlyExpenses);

                // 9. Save all changes to the database.
                if (!await _unitOfWork.SaveAsync(cancellationToken))
                {
                    throw new Exception("Failed to save DailyExpenses (and attachment, if any) to the database.");
                }

                return dailyExpense.Id;
            }
            catch (Exception ex)
            {
                // Optionally, wrap exceptions in a custom HandlerException if you have one.
                throw new Exception($"An error occurred while adding daily expense: {ex.Message}", ex);
            }
        }
    }
}