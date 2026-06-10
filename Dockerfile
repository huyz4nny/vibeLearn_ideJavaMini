# ==========================================
# GIAI ĐOẠN 1: BUILD VÀ PUBLISH ỨNG DỤNG
# ==========================================
# Sử dụng image SDK của .NET 8 chứa đầy đủ công cụ để biên dịch mã nguồn
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Sao chép duy nhất file project (.csproj) vào trước để khôi phục (restore) dependencies.
# Bước này tận dụng cơ chế lưu cache layer của Docker: nếu file .csproj không đổi, 
# Docker sẽ bỏ qua việc chạy lại dotnet restore trong những lần build sau, giúp tăng tốc build.
COPY ["testVibeCode.csproj", "./"]
RUN dotnet restore "testVibeCode.csproj"

# Sao chép toàn bộ mã nguồn vào thư mục làm việc trong container
COPY . .

# Biên dịch ứng dụng ở chế độ Release (tối ưu hóa code) và xuất file ra thư mục /app/build
RUN dotnet build "testVibeCode.csproj" -c Release -o /app/build

# Thực hiện publish ứng dụng (gom các file .dll chạy chính, cấu hình và static files)
# Thiết lập /p:UseAppHost=false để không gen ra file execute (.exe) cho OS hiện tại, 
# vì chúng ta chạy ứng dụng thông qua lệnh dotnet CLI.
FROM build AS publish
RUN dotnet publish "testVibeCode.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# GIAI ĐOẠN 2: CHẠY ỨNG DỤNG (RUNTIME)
# ==========================================
# Sử dụng image ASP.NET Core Runtime 8.0 siêu nhẹ (chỉ khoảng 200MB thay vì SDK hơn 800MB)
# Điều này giúp tối ưu hóa dung lượng disk và tăng tốc độ deploy trên Railway.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Khai báo port mà container sẽ listen (80 cho HTTP và 443 cho HTTPS)
EXPOSE 80
EXPOSE 443

# Sao chép toàn bộ các file đã được publish từ giai đoạn 1 sang runtime container này
COPY --from=publish /app/publish .

# Thiết lập biến môi trường chỉ định Kestrel (Web Server nội bộ của .NET) listen trên cổng 80.
# Railway sẽ map cổng public của họ vào cổng 80 của container này.
ENV ASPNETCORE_URLS=http://+:80

# Lệnh khởi chạy ứng dụng khi container bắt đầu chạy.
# testVibeCode.dll là file Assembly chính chứa toàn bộ mã nguồn đã biên dịch.
ENTRYPOINT ["dotnet", "testVibeCode.dll"]
