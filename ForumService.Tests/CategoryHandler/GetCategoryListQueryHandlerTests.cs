using AutoMapper;
using ForumService.Contract.TransferObjects.Category;
using ForumService.Core.Handler.Category.Query;
using ForumService.Core.Interfaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Category.Query;

namespace ForumService.Tests.CategoryHandler
{
    public class GetCategoryListQueryHandlerTests
    {
        private readonly Mock<IGenericRepository<Domain.Models.Category>> _categoryRepoMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly GetCategoryListQueryHandler _handler;

        public GetCategoryListQueryHandlerTests()
        {
            _categoryRepoMock = new Mock<IGenericRepository<Domain.Models.Category>>();
            _mapperMock = new Mock<IMapper>();

            _handler = new GetCategoryListQueryHandler(
                _categoryRepoMock.Object,
                _mapperMock.Object
            );
        }

        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ReturnsSuccessWithMultipleCategories()
        {
            // Arrange
            var query = new GetCategoryListQuery(null, null, 20, 1);

            var categoriesDomain = new List<Domain.Models.Category>
            {
                new Domain.Models.Category { CategoryId = Guid.NewGuid(), Name = "Category 1" },
                new Domain.Models.Category { CategoryId = Guid.NewGuid(), Name = "Category 2" }
            };

            var categoriesDto = new List<CategoryDto>
            {
                new CategoryDto { Name = "Category 1" },
                new CategoryDto { Name = "Category 2" }
            };

            // Setup Count
            _categoryRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Domain.Models.Category, bool>>>()))
                .ReturnsAsync(2);

            // Setup List Retrieval 
            _categoryRepoMock.Setup(r => r.GetListAsyncUntracked(
                    It.IsAny<Expression<Func<Domain.Models.Category, bool>>>(), // 1. filter
                    It.IsAny<Expression<Func<IQueryable<Domain.Models.Category>, IOrderedQueryable<Domain.Models.Category>>>>(), // 2. orderBy (Expression)
                    It.IsAny<Expression<Func<Domain.Models.Category, Domain.Models.Category>>>(), // 3. selector
                    It.IsAny<Expression<Func<IQueryable<Domain.Models.Category>, IQueryable<Domain.Models.Category>>>>(), // 4. include (NEW)
                    It.IsAny<int?>(), // 5. pageSize
                    It.IsAny<int?>()  // 6. pageNumber
                ))
                .ReturnsAsync(categoriesDomain);

            _mapperMock.Setup(m => m.Map<IEnumerable<CategoryDto>>(categoriesDomain))
                .Returns(categoriesDto);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Equal(2, result.ResponseData.Count());
            Assert.Equal(2, result.Pagination.TotalItems);
        }

        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ReturnsSuccessWithEmptyList()
        {
            // Arrange
            var query = new GetCategoryListQuery(null, null, 20, 1);

            _categoryRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Domain.Models.Category, bool>>>()))
                .ReturnsAsync(0);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Empty(result.ResponseData);
            Assert.Equal("No categories found.", result.Message);

            // Verify method was NOT called 
            _categoryRepoMock.Verify(r => r.GetListAsyncUntracked(
                    It.IsAny<Expression<Func<Domain.Models.Category, bool>>>(),
                    It.IsAny<Expression<Func<IQueryable<Domain.Models.Category>, IOrderedQueryable<Domain.Models.Category>>>>(),
                    It.IsAny<Expression<Func<Domain.Models.Category, Domain.Models.Category>>>(),
                    It.IsAny<Expression<Func<IQueryable<Domain.Models.Category>, IQueryable<Domain.Models.Category>>>>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>()
                ), Times.Never);
        }

        [Fact]
        [Trait("Category", "Handler - Filter")]
        public async Task Handle_WithSearchKeyword_AppliesFilter()
        {
            // Arrange
            var query = new GetCategoryListQuery("Tech", null, 20, 1);
            var categoriesDomain = new List<Domain.Models.Category> { new Domain.Models.Category() };

            _categoryRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Domain.Models.Category, bool>>>()))
                .ReturnsAsync(1);

            _categoryRepoMock.Setup(r => r.GetListAsyncUntracked(
                    It.IsAny<Expression<Func<Domain.Models.Category, bool>>>(),
                    It.IsAny<Expression<Func<IQueryable<Domain.Models.Category>, IOrderedQueryable<Domain.Models.Category>>>>(),
                    It.IsAny<Expression<Func<Domain.Models.Category, Domain.Models.Category>>>(),
                    It.IsAny<Expression<Func<IQueryable<Domain.Models.Category>, IQueryable<Domain.Models.Category>>>>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>()))
                .ReturnsAsync(categoriesDomain);

            _mapperMock.Setup(m => m.Map<IEnumerable<CategoryDto>>(categoriesDomain))
                .Returns(new List<CategoryDto> { new CategoryDto() });

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            _categoryRepoMock.Verify(r => r.GetCountAsync(It.IsNotNull<Expression<Func<Domain.Models.Category, bool>>>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "Handler - Filter")]
        public async Task Handle_WithIsActiveFilter_AppliesFilter()
        {
            // Arrange
            var query = new GetCategoryListQuery(null, true, 20, 1);
            var categoriesDomain = new List<Domain.Models.Category> { new Domain.Models.Category() };

            _categoryRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Domain.Models.Category, bool>>>()))
                .ReturnsAsync(1);

            _categoryRepoMock.Setup(r => r.GetListAsyncUntracked(
                   It.IsAny<Expression<Func<Domain.Models.Category, bool>>>(),
                   It.IsAny<Expression<Func<IQueryable<Domain.Models.Category>, IOrderedQueryable<Domain.Models.Category>>>>(),
                   It.IsAny<Expression<Func<Domain.Models.Category, Domain.Models.Category>>>(),
                   It.IsAny<Expression<Func<IQueryable<Domain.Models.Category>, IQueryable<Domain.Models.Category>>>>(),
                   It.IsAny<int?>(),
                   It.IsAny<int?>()))
               .ReturnsAsync(categoriesDomain);

            _mapperMock.Setup(m => m.Map<IEnumerable<CategoryDto>>(categoriesDomain))
                .Returns(new List<CategoryDto> { new CategoryDto() });

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            _categoryRepoMock.Verify(r => r.GetCountAsync(It.IsNotNull<Expression<Func<Domain.Models.Category, bool>>>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "Handler - Exception")]
        public async Task Handle_ThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var query = new GetCategoryListQuery(null, null, 20, 1);

            _categoryRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Domain.Models.Category, bool>>>()))
                .ThrowsAsync(new Exception("Database connection failed"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Null(result.ResponseData);
            Assert.Contains("Failed to retrieve categories: Database connection failed", result.Message);
        }

        [Fact]
        [Trait("Category", "Handler - Logic")]
        public async Task Handle_InvalidPagination_UsesDefaultValues()
        {
            // Arrange
            var query = new GetCategoryListQuery(null, null, 0, -5);

            _categoryRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Domain.Models.Category, bool>>>()))
                .ReturnsAsync(5);

            _categoryRepoMock.Setup(r => r.GetListAsyncUntracked(
                   It.IsAny<Expression<Func<Domain.Models.Category, bool>>>(),
                   It.IsAny<Expression<Func<IQueryable<Domain.Models.Category>, IOrderedQueryable<Domain.Models.Category>>>>(),
                   It.IsAny<Expression<Func<Domain.Models.Category, Domain.Models.Category>>>(),
                   It.IsAny<Expression<Func<IQueryable<Domain.Models.Category>, IQueryable<Domain.Models.Category>>>>(),
                   It.IsAny<int?>(),
                   It.IsAny<int?>()))
               .ReturnsAsync(new List<Domain.Models.Category>());

            _mapperMock.Setup(m => m.Map<IEnumerable<CategoryDto>>(It.IsAny<IEnumerable<Domain.Models.Category>>()))
                .Returns(new List<CategoryDto>());

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result.Pagination);

            // Check that defaults were applied
            Assert.Equal(1, result.Pagination.Page);
            Assert.Equal(10, result.Pagination.PageSize);

            // Verify correct values passed to Repository (Arg 5 is pageSize, Arg 6 is pageNumber)
            _categoryRepoMock.Verify(r => r.GetListAsyncUntracked(
                    It.IsAny<Expression<Func<Domain.Models.Category, bool>>>(),
                    It.IsAny<Expression<Func<IQueryable<Domain.Models.Category>, IOrderedQueryable<Domain.Models.Category>>>>(),
                    It.IsAny<Expression<Func<Domain.Models.Category, Domain.Models.Category>>>(),
                    It.IsAny<Expression<Func<IQueryable<Domain.Models.Category>, IQueryable<Domain.Models.Category>>>>(),
                    10, // Check PageSize
                    1   // Check PageNumber
                ), Times.Once);
        }

        [Fact]
        [Trait("Category", "Handler - Logic")]
        public async Task Handle_WithComplexSearchString_RunsSuccessfully()
        {
            // Arrange
            var query = new GetCategoryListQuery("Tiếng Việt @#$", null, 20, 1);

            _categoryRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Domain.Models.Category, bool>>>()))
                .ReturnsAsync(0);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
        }
    }
}
