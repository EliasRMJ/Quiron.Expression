## What is the Quiron.Expression?

Package used to convert filters coming from a 'ViewModel' coming from the controller to an expression recognized by one understood in the 'EntityFrameworkCore'.

## Give a Star! ⭐

If you find this project useful, please give it a star! It helps us grow and improve the community.

## Namespaces and Dependencies

- ✅ Quiron.Expression
- ✅ System.Linq.Expressions

## Methods

- ✅ Builder
- ✅ ConvertIncludesExpression

## Some Basic Examples

### Filter Convert
```csharp
var list = new List<ViewModel>
{
	new ViewModel { Id = 0, Date = DateTime.Now },
	new ViewModel { Id = 1, Date = DateTime.Now.AddDays(1) }
};
Expression<Func<ViewModel, bool>> filter = list.Where(e => e.Id == 0 && e.Date = DateTime.Now);

var filterConverted = ExpressionConvert.Builder<ViewModel, Entity>(expression);
```

## Usage Reference

For more details, access the test project that has the practical implementation of the package's use.

https://github.com/EliasRMJ/Quiron.EntityFrameworkCore.Test

Supports:

- ✅ .NET Standard 2.1  
- ✅ .NET 9 through 9 (including latest versions)  
- ⚠️ Legacy support for .NET Core 3.1 and older (with limitations)
  
## About
Quiron.Expression was developed by [EliasRMJ](https://www.linkedin.com/in/elias-medeiros-98232066/) under the [MIT license](LICENSE).
