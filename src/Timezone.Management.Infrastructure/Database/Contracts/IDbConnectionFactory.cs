using System.Data;

namespace Timezone.Management.Infrastructure.Database.Contracts;

public interface IDbConnectionFactory
{
	IDbConnection CreateConnection();
}