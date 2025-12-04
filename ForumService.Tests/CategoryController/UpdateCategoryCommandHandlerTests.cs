using ForumService.Core.Handler.Category.Command;
using ForumService.Core.Interfaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Category.Command;

namespace ForumService.Tests.CategoryController
{
    public class UpdateCategoryCommandHandlerTests
    {
        // Mocks
        private readonly Mock<IGenericRepository<Domain.Models.Category>> _categoryRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;

        // System Under Test
        private readonly UpdateCategoryCommandHandler _handler;

        public UpdateCategoryCommandHandlerTests()
        {
            _categoryRepoMock = new Mock<IGenericRepository<Domain.Models.Category>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            _handler = new UpdateCategoryCommandHandler(
                _categoryRepoMock.Object,
                _unitOfWorkMock.Object
            );
        }

        // Test Case 1: Validation - Empty ID
        // Scenario: CategoryId is Guid.Empty.
        // Expected: Returns 400 Bad Request.
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_EmptyCategoryId_ReturnsBadRequest()
        {
            // Arrange
            var command = new UpdateCategoryCommand(Guid.Empty, "New Name", "Desc", true);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Equal("Invalid CategoryId.", result.Message);
            Assert.False(result.ResponseData);

            // Verify Repo not called
            _categoryRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
        }

        // Test Case 2: Not Found
        // Scenario: ID is valid, but Repo returns null.
        // Expected: Returns 404 Not Found.
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_CategoryNotFound_ReturnsNotFound()
        {
            // Arrange
            var command = new UpdateCategoryCommand(Guid.NewGuid(), "New Name", "Desc", true);

            _categoryRepoMock.Setup(r => r.GetByIdAsync(command.CategoryId))
                .ReturnsAsync((Domain.Models.Category?)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Equal("Category not found.", result.Message);
            Assert.False(result.ResponseData);

            // Verify Update flow not started
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Never);
        }

        // Test Case 3: Happy Path - Success
        // Scenario: Category exists, valid data provided.
        // Expected: Updates properties (Name, Slug, UpdatedAt), Commits Transaction.
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ValidRequest_UpdatesCategorySuccessfully()
        {
            // Arrange
            var categoryId = Guid.NewGuid();
            var existingCategory = new Domain.Models.Category
            {
                CategoryId = categoryId,
                Name = "Old Name",
                Slug = "old-name",
                Description = "Old Desc",
                IsActive = false,
                UpdatedAt = DateTime.UtcNow.AddDays(-1) // Old date
            };

            // Input command with Vietnamese characters to test Slug generation
            var command = new UpdateCategoryCommand(categoryId, "Tin Tức Mới", "New Description", true);

            _categoryRepoMock.Setup(r => r.GetByIdAsync(categoryId))
                .ReturnsAsync(existingCategory);

            // Capture the entity passed to UpdateAsync
            Domain.Models.Category capturedCategory = null;
            _categoryRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Domain.Models.Category>()))
                .Callback<Domain.Models.Category>(c => capturedCategory = c)
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Equal("Category updated successfully.", result.Message);
            Assert.True(result.ResponseData);

            // Verify Data Changes
            Assert.NotNull(capturedCategory);
            Assert.Equal("Tin Tức Mới", capturedCategory.Name);
            Assert.Equal("tin-tuc-moi", capturedCategory.Slug); // Generated Slug
            Assert.Equal("New Description", capturedCategory.Description);
            Assert.True(capturedCategory.IsActive);

            // Verify UpdatedAt is recent (within last second)
            Assert.True((DateTime.UtcNow - capturedCategory.UpdatedAt.Value).TotalSeconds < 1);

            // Verify Transaction Flow
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _categoryRepoMock.Verify(r => r.UpdateAsync(existingCategory), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        // Test Case 4: Exception Handling (Rollback)
        // Scenario: SaveChangesAsync throws exception.
        // Expected: Returns 500, Rolls back transaction.
        [Fact]
        [Trait("Category", "Handler - Exception")]
        public async Task Handle_ExceptionDuringSave_RollsBackTransaction()
        {
            // Arrange
            var categoryId = Guid.NewGuid();
            var command = new UpdateCategoryCommand(categoryId, "Name", "Desc");
            var existingCategory = new Domain.Models.Category { CategoryId = categoryId };

            _categoryRepoMock.Setup(r => r.GetByIdAsync(categoryId))
                .ReturnsAsync(existingCategory);

            // Setup SaveChanges to fail
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync())
                .ThrowsAsync(new Exception("Database deadlock"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.StartsWith("Failed to update category: Database deadlock", result.Message);
            Assert.False(result.ResponseData);

            // Verify Rollback
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once); // Must be called
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Never); // Must NOT be called
        }
    }
}
