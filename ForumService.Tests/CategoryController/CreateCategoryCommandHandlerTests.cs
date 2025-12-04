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
    public class CreateCategoryCommandHandlerTests
    {
        // Mocks for dependencies
        private readonly Mock<IGenericRepository<Domain.Models.Category>> _categoryRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;

        // System Under Test
        private readonly CreateCategoryCommandHandler _handler;

        public CreateCategoryCommandHandlerTests()
        {
            _categoryRepoMock = new Mock<IGenericRepository<Domain.Models.Category>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            _handler = new CreateCategoryCommandHandler(
                _categoryRepoMock.Object,
                _unitOfWorkMock.Object
            );
        }

        // Test Case 1: Validation Failure (Empty Name)
        // Scenario: Input Name is null or whitespace.
        // Expected: Returns 400 Bad Request, Repository is not called.
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task Handle_EmptyName_ReturnsBadRequest(string invalidName)
        {
            // Arrange
            var command = new CreateCategoryCommand(invalidName, "Description", true);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Status);
            Assert.Equal("Category name cannot be empty.", result.Message);
            Assert.False(result.ResponseData);

            // Verify logic stops before touching DB
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Never);
            _categoryRepoMock.Verify(r => r.AddAsync(It.IsAny<Domain.Models.Category>()), Times.Never);
        }

        // Test Case 2: Happy Path - Success
        // Scenario: Valid input.
        // Expected: Transaction flow completes, AddAsync called with correct data, Returns 200.
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ValidRequest_CreatesCategorySuccessfully()
        {
            // Arrange
            var command = new CreateCategoryCommand("Tech News", "All about tech", true);

            // Capture the entity passed to AddAsync to verify properties
            Domain.Models.Category capturedCategory = null;
            _categoryRepoMock.Setup(r => r.AddAsync(It.IsAny<Domain.Models.Category>()))
                .Callback<Domain.Models.Category>(c => capturedCategory = c)
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert Response
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.Equal("Category created successfully.", result.Message);
            Assert.True(result.ResponseData);

            // Verify Transaction Flow
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _categoryRepoMock.Verify(r => r.AddAsync(It.IsAny<Domain.Models.Category>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);

            // Verify Data Mapping & Slug Generation
            Assert.NotNull(capturedCategory);
            Assert.NotEqual(Guid.Empty, capturedCategory.CategoryId);
            Assert.Equal("Tech News", capturedCategory.Name);
            Assert.Equal("tech-news", capturedCategory.Slug); // Verify simple slug
            Assert.Equal("All about tech", capturedCategory.Description);
            Assert.True(capturedCategory.IsActive);
            Assert.NotEqual(default, capturedCategory.CreatedAt);
            Assert.NotEqual(default, capturedCategory.UpdatedAt);
        }

        // Test Case 3: Slug Generation Logic (Complex/Vietnamese)
        // Scenario: Name contains Vietnamese characters and special symbols.
        // Expected: Slug is normalized (removed accents, lowercase, removed special chars).
        [Fact]
        [Trait("Category", "Handler - Logic")]
        public async Task Handle_VietnameseName_GeneratesCorrectSlug()
        {
            // Arrange
            // "Đời sống & Xã hội" -> Expected: "doi-song-xa-hoi"
            var command = new CreateCategoryCommand("Đời sống & Xã hội @ 2024", "Desc", true);

            Domain.Models.Category capturedCategory = null;
            _categoryRepoMock.Setup(r => r.AddAsync(It.IsAny<Domain.Models.Category>()))
                .Callback<Domain.Models.Category>(c => capturedCategory = c);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedCategory);
            // Verify normalization: Đ -> d, spaces -> -, & -> removed
            Assert.Equal("doi-song-xa-hoi-2024", capturedCategory.Slug);
        }

        // Test Case 4: Exception Handling (Rollback)
        // Scenario: Repository throws exception during AddAsync.
        // Expected: Returns 500, Transaction is Rolled Back.
        [Fact]
        [Trait("Category", "Handler - Exception")]
        public async Task Handle_RepositoryThrowException_RollsBackTransaction()
        {
            // Arrange
            var command = new CreateCategoryCommand("Error Category", "Desc");

            // Setup failure
            _categoryRepoMock.Setup(r => r.AddAsync(It.IsAny<Domain.Models.Category>()))
                .ThrowsAsync(new Exception("Database connection failed"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(500, result.Status);
            Assert.False(result.ResponseData);
            Assert.StartsWith("Failed to create category: Database connection failed", result.Message);

            // Verify Transaction Flow
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once); // Started
            _categoryRepoMock.Verify(r => r.AddAsync(It.IsAny<Domain.Models.Category>()), Times.Once); // Attempted add
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Never); // Never committed
            _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once); // ROLLED BACK
        }

        // Test Case 5: Exception Handling (Save Changes Error)
        // Scenario: AddAsync succeeds, but SaveChangesAsync fails.
        // Expected: Returns 500, Transaction is Rolled Back.
        [Fact]
        [Trait("Category", "Handler - Exception")]
        public async Task Handle_SaveChangesThrowsException_RollsBackTransaction()
        {
            // Arrange
            var command = new CreateCategoryCommand("Save Error", "Desc");

            // Setup SaveChanges to fail
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync())
                .ThrowsAsync(new Exception("Constraint violation"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);

            // Verify Rollback called
            _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Never);
        }
    }
}
