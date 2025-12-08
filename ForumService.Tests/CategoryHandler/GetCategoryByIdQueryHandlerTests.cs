using AutoMapper;
using ForumService.Contract.TransferObjects.Category;
using ForumService.Core.Handler.Category.Query;
using ForumService.Core.Interfaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Category.Query;

namespace ForumService.Tests.CategoryHandler
{
    public class GetCategoryByIdQueryHandlerTests
    {
        // Mocks for dependencies
        private readonly Mock<IGenericRepository<Domain.Models.Category>> _categoryRepoMock;
        private readonly Mock<IMapper> _mapperMock;

        // System Under Test
        private readonly GetCategoryByIdQueryHandler _handler;

        // Common Test Data
        private readonly Guid _validCategoryId = Guid.Parse("8cf071b9-ea2e-4a19-865e-28ec04a26ba7");

        public GetCategoryByIdQueryHandlerTests()
        {
            _categoryRepoMock = new Mock<IGenericRepository<Domain.Models.Category>>();
            _mapperMock = new Mock<IMapper>();

            _handler = new GetCategoryByIdQueryHandler(
                _categoryRepoMock.Object,
                _mapperMock.Object
            );
        }

        // Test Case 1: Happy Path - Valid ID found
        // Scenario: Repo returns an entity, Handler maps it to DTO.
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ValidId_ReturnsSuccess()
        {
            // Arrange
            var query = new GetCategoryByIdQuery(_validCategoryId);

            var categoryDomain = new Domain.Models.Category
            {
                CategoryId = _validCategoryId,
                Name = "Category 1",
                IsActive = true
            };

            var categoryDto = new CategoryDto
            {
                CategoryId = _validCategoryId,
                Name = "Category 1",
                IsActive = true
            };

            // Setup Repo to return the domain entity
            _categoryRepoMock.Setup(r => r.GetByIdAsync(_validCategoryId))
                .ReturnsAsync(categoryDomain);

            // Setup Mapper to return the DTO
            _mapperMock.Setup(m => m.Map<CategoryDto>(categoryDomain))
                .Returns(categoryDto);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.Equal(_validCategoryId, result.ResponseData.CategoryId);
            Assert.Equal("Category retrieved successfully.", result.Message);
        }

        // Test Case 2: Not Found
        // Scenario: Repo returns null.
        // Expected: Handler returns 404 Status.
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_NonExistentId_ReturnsNotFound()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();
            var query = new GetCategoryByIdQuery(nonExistentId);

            // Setup Repo to return null
            _categoryRepoMock.Setup(r => r.GetByIdAsync(nonExistentId))
                .ReturnsAsync((Domain.Models.Category?)null);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(404, result.Status);
            Assert.Null(result.ResponseData);
            Assert.Equal("Category not found.", result.Message);
        }

        // Test Case 3: Validation Logic (Empty ID)
        // Scenario: Query contains Guid.Empty.
        // Expected: Handler returns 400 Status (Logic defined at start of Handler).
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_EmptyId_ReturnsBadRequest()
        {
            // Arrange
            var query = new GetCategoryByIdQuery(Guid.Empty);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Status);
            Assert.Null(result.ResponseData);
            Assert.Equal("Invalid CategoryId.", result.Message);

            // Verify Repo was NOT called (Optimization check)
            _categoryRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<object>()), Times.Never);
        }

        // Test Case 4: Exception Handling
        // Scenario: Repository throws a database exception.
        // Expected: Handler catches exception and returns 500.
        [Fact]
        [Trait("Category", "Handler - Exception")]
        public async Task Handle_ThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var query = new GetCategoryByIdQuery(_validCategoryId);

            _categoryRepoMock.Setup(r => r.GetByIdAsync(_validCategoryId))
                .ThrowsAsync(new Exception("Database timeout"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(500, result.Status);
            Assert.Null(result.ResponseData);
            Assert.StartsWith("Failed to retrieve category: Database timeout", result.Message);
        }

        // Test Case 5: Mapping Logic Check (Inactive Category)
        // Scenario: Repo returns inactive category.
        // Expected: Handler maps IsActive correctly.
        [Fact]
        [Trait("Category", "Handler - Mapping")]
        public async Task Handle_InactiveCategory_ReturnsSuccessWithFalseFlag()
        {
            // Arrange
            var query = new GetCategoryByIdQuery(_validCategoryId);

            var categoryDomain = new Domain.Models.Category { IsActive = false };
            var categoryDto = new CategoryDto { IsActive = false };

            _categoryRepoMock.Setup(r => r.GetByIdAsync(_validCategoryId))
                .ReturnsAsync(categoryDomain);
            _mapperMock.Setup(m => m.Map<CategoryDto>(categoryDomain))
                .Returns(categoryDto);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.False(result.ResponseData.IsActive);
        }

        // Test Case 6: Mapping Logic Check (Null Fields)
        // Scenario: Description and UpdatedAt are null in DB.
        // Expected: DTO reflects nulls.
        [Fact]
        [Trait("Category", "Handler - Mapping")]
        public async Task Handle_CategoryWithNullFields_ReturnsSuccess()
        {
            // Arrange
            var query = new GetCategoryByIdQuery(_validCategoryId);

            var categoryDomain = new Domain.Models.Category
            {
                Description = null,
                UpdatedAt = null
            };
            var categoryDto = new CategoryDto
            {
                Description = null,
                UpdatedAt = null
            };

            _categoryRepoMock.Setup(r => r.GetByIdAsync(_validCategoryId))
                .ReturnsAsync(categoryDomain);
            _mapperMock.Setup(m => m.Map<CategoryDto>(categoryDomain))
                .Returns(categoryDto);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Null(result.ResponseData.Description);
            Assert.Null(result.ResponseData.UpdatedAt);
        }
    }
}
