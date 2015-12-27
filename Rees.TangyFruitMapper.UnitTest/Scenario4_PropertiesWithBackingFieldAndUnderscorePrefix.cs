﻿using Rees.TangyFruitMapper.UnitTest.TestData;
using Xunit;
using Xunit.Abstractions;

namespace Rees.TangyFruitMapper.UnitTest
{
    public class Scenario4_PropertiesWithBackingFieldAndUnderscorePrefix : MappingGeneratorScenarios<DtoType4, ModelType4_UnderscoreBackingField>
    {
        public Scenario4_PropertiesWithBackingFieldAndUnderscorePrefix(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate_ShouldOutputCode()
        {
            Assert.NotEmpty(this.generatedCode);
        }

        [Fact]
        public void Generate_ShouldSuccessfullyMapToDto()
        {
            var mapper = CreateMapper();
            var result = mapper.ToDto(new ModelType4_UnderscoreBackingField(410, 3.1415M, "Pie Constant"));

            Assert.Equal(410, result.Age);
            Assert.Equal(3.1415M, result.MyNumber);
            Assert.Equal("Pie Constant", result.Name);
        }

        [Fact]
        public void Generate_ShouldSuccessfullyMapToModel()
        {
            var mapper = CreateMapper();
            var result = mapper.ToModel(new DtoType4
            {
                Age = 410,
                MyNumber = 3.1415M,
                Name = "Pie Constant"
            });

            Assert.Equal(410, result.Age);
            Assert.Equal(3.1415M, result.MyNumber);
            Assert.Equal("Pie Constant", result.Name);
        }

    }
}
