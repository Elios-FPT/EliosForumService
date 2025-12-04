using ForumService.Core.Handler.Category.Command;
using ForumService.Core.Interfaces;
using Moq;
using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Category.Command;

namespace ForumService.Tests.CategoryController
{
    public class DeleteCategoryCommandHandlerTests
    {
        // Mocks
        private readonly Mock<IGenericRepository<Domain.Models.Category>> _categoryRepoMock;
        private readonly Mock<IGenericRepository<Domain.Models.Post>> _postRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;

        // System Under Test
        private readonly DeleteCategoryCommandHandler _handler;

        public DeleteCategoryCommandHandlerTests()
        {
            _categoryRepoMock = new Mock<IGenericRepository<Domain.Models.Category>>();
            _postRepoMock = new Mock<IGenericRepository<Domain.Models.Post>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            _handler = new DeleteCategoryCommandHandler(
                _categoryRepoMock.Object,
                _postRepoMock.Object,
                _unitOfWorkMock.Object
            );
        }

        // Test Case 1: Category Not Found
        // Scenario: GetByIdAsync returns null.
        // Expected: Returns 404, does not check posts, does not attempt delete.
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_CategoryNotFound_ReturnsNotFound()
        {
            // Arrange
            var command = new DeleteCategoryCommand(Guid.NewGuid());

            _categoryRepoMock.Setup(r => r.GetByIdAsync(command.CategoryId))
                .ReturnsAsync((Domain.Models.Category?)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Equal("Category not found.", result.Message);
            Assert.False(result.ResponseData);

            // Verify dependencies were NOT called
            _postRepoMock.Verify(r => r.GetCountAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>()), Times.Never);
            _categoryRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Domain.Models.Category>()), Times.Never);
        }

        // Test Case 2: Business Rule - Category In Use
        // Scenario: Category exists, but Post Repository returns Count > 0.
        // Expected: Returns 400 Bad Request.
        [Fact]
        [Trait("Category", "Handler - BusinessRule")]
        public async Task Handle_CategoryHasActivePosts_ReturnsBadRequest()
        {
            // Arrange
            var categoryId = Guid.NewGuid();
            var command = new DeleteCategoryCommand(categoryId);
            var category = new Domain.Models.Category { CategoryId = categoryId };

            // Setup Category exists
            _categoryRepoMock.Setup(r => r.GetByIdAsync(categoryId))
                .ReturnsAsync(category);

            // Setup Posts exist (Count = 5)
            // Note: We use It.IsAny for the expression because Moq cannot easily match specific Lambda expressions
            _postRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>()))
                .ReturnsAsync(5);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Contains("being used by 5 active post(s)", result.Message);
            Assert.False(result.ResponseData);

            // Verify Delete was NEVER called
            _categoryRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Domain.Models.Category>()), Times.Never);
        }

        // Test Case 3: Happy Path - Success
        // Scenario: Category exists, No active posts.
        // Expected: Returns 200, Transaction Commits.
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ValidRequest_DeletesCategorySuccessfully()
        {
            // Arrange
            var categoryId = Guid.NewGuid();
            var command = new DeleteCategoryCommand(categoryId);
            var category = new Domain.Models.Category { CategoryId = categoryId };

            // Setup Category exists
            _categoryRepoMock.Setup(r => r.GetByIdAsync(categoryId))
                .ReturnsAsync(category);

            // Setup No Active Posts (Count = 0)
            _postRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>()))
                .ReturnsAsync(0);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Equal("Category deleted successfully.", result.Message);
            Assert.True(result.ResponseData);

            // Verify Transaction Flow
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _categoryRepoMock.Verify(r => r.DeleteAsync(category), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        // Test Case 4: Exception Handling (Rollback)
        // Scenario: DeleteAsync or SaveChangesAsync throws exception.
        // Expected: Returns 500, Transaction Rolls Back.
        [Fact]
        [Trait("Category", "Handler - Exception")]
        public async Task Handle_ExceptionOccurs_RollsBackTransaction()
        {
            // Arrange
            var categoryId = Guid.NewGuid();
            var command = new DeleteCategoryCommand(categoryId);
            var category = new Domain.Models.Category { CategoryId = categoryId };

            _categoryRepoMock.Setup(r => r.GetByIdAsync(categoryId))
                .ReturnsAsync(category);

            _postRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>()))
                .ReturnsAsync(0);

            // Setup Exception during Delete
            _categoryRepoMock.Setup(r => r.DeleteAsync(category))
                .ThrowsAsync(new Exception("Foreign key constraint failed"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.StartsWith("Failed to delete category:", result.Message);
            Assert.False(result.ResponseData);

            // Verify Rollback
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once); // Important
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Never); // Ensure no commit happened
        }
    }
}
